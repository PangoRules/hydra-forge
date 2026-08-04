using System.Collections.Concurrent;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Realtime;

[Authorize]
[EnableRateLimiting("SignalR")]
public class ChatHub(
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo,
    IChatSessionRepository sessionRepo,
    IChatMessageRepository messageRepo,
    IPromptPresetRepository presetRepo,
    IAgentPersonalityRepository personalityRepo,
    IChatRagRetriever ragRetriever,
    IModelRouter modelRouter,
    ILlmClientFactory llmClientFactory,
    IUsageRecorder usageRecorder,
    LlmCallGuard llmCallGuard,
    IContextCompressor contextCompressor,
    IChatTitleGenerator titleGenerator,
    ILogger<ChatHub> logger,
    IOptions<LlmOptions> llmOptions
) : Hub<IChatHub>
{
    private static readonly ConcurrentDictionary<Guid, StreamContext> ActiveStreams = new();

    private static string SessionGroup(Guid sessionId) => $"chat-{sessionId}";

    private record StreamContext(CancellationTokenSource Cts, Guid MessageId);

    private async Task<bool> HasSessionAccessAsync(
        Domain.Entities.Chat.ChatSession session,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (session.OwnerId == userId)
            return true;

        if (session.ProjectId.HasValue && session.IsShared)
            return await MembershipGuard.HasAccessAsync(
                userRepo,
                memberRepo,
                session.ProjectId.Value,
                userId,
                ct
            );

        return false;
    }

    public async Task JoinSession(Guid sessionId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            throw new HubException(DomainErrorCodes.Chat.SessionNotFound);

        if (!await HasSessionAccessAsync(session, userId))
            throw new HubException(DomainErrorCodes.Chat.SessionNotOwner);

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task SendMessage(
        Guid sessionId,
        Guid userMessageId,
        Guid? presetId,
        Guid? preferredProviderModelConfigId = null
    )
    {
        var userId = Context.User!.GetRequiredUserId();

        var userMessage = await messageRepo.GetByIdAsync(userMessageId);
        if (
            userMessage is null
            || userMessage.SessionId != sessionId
            || userMessage.Role != MessageRole.User
        )
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.MessageNotFound,
                    "Message not found"
                );
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.SessionNotFound,
                    "Session not found"
                );
            return;
        }

        if (!await HasSessionAccessAsync(session, userId))
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "You do not have access to this session"
                );
            return;
        }

        if (session.Status != ChatSessionStatus.Active)
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.SessionClosed,
                    "Session is closed"
                );
            return;
        }

        if (session.ArchivedAt.HasValue)
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.SessionArchived,
                    "Session has been archived"
                );
            return;
        }

        var cts = new CancellationTokenSource();
        var assistantMessageId = Guid.NewGuid();

        if (!ActiveStreams.TryAdd(sessionId, new StreamContext(cts, assistantMessageId)))
        {
            cts.Dispose();
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    userMessageId,
                    DomainErrorCodes.Chat.StreamInProgress,
                    "A stream is already active for this session"
                );
            return;
        }

        try
        {
            IReadOnlyList<CacheBlock> ragBlocks = [];
            try
            {
                ragBlocks = await ragRetriever.RetrieveAsync(
                    sessionId,
                    userMessage.Content,
                    session.SearchAllMyDocs,
                    llmOptions.Value?.Rag?.TopK ?? 8,
                    cts.Token
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "RAG retrieval failed for session {SessionId}, proceeding without context",
                    sessionId
                );
            }

            var chatMessages = new List<ChatMessage>();

            if (session.PersonalityId.HasValue)
            {
                var personality = await personalityRepo.GetByIdAsync(session.PersonalityId.Value);
                if (personality is { ArchivedAt: null })
                {
                    chatMessages.Add(new ChatMessage(ChatRole.System, personality.SystemPrompt));
                }
            }

            string? presetContent = null;
            if (presetId.HasValue)
            {
                var preset = await presetRepo.GetByIdAsync(presetId.Value);
                if (preset is { ArchivedAt: null } && preset.UserId == userId)
                {
                    presetContent =
                        $"<preset>\n{preset.Content}\n</preset>\n\n{userMessage.Content}";
                }
            }

            var history = (
                await messageRepo.GetBySessionAsync(sessionId, null, null, 100, cts.Token)
            )
                .Reverse()
                .ToList();
            var isFirstMessage = history.Count == 1;
            foreach (var msg in history)
            {
                var role = msg.Role switch
                {
                    MessageRole.User => ChatRole.User,
                    MessageRole.Assistant => ChatRole.Assistant,
                    MessageRole.System => ChatRole.System,
                    MessageRole.Tool => ChatRole.Tool,
                    _ => ChatRole.User,
                };

                var images = ChatMessageMapper.ToApplicationImages(msg.ImagesJson);

                if (presetId.HasValue && msg.Id == userMessageId && presetContent is not null)
                {
                    chatMessages.Add(
                        new ChatMessage(role, presetContent, images.Length > 0 ? images : null)
                    );
                }
                else
                {
                    chatMessages.Add(
                        new ChatMessage(role, msg.Content, images.Length > 0 ? images : null)
                    );
                }
            }

            var feature = session.ProjectId.HasValue
                ? AiFeature.ProjectChat
                : AiFeature.PersonalChat;
            var historyContent = string.Join("\n", history.Select(m => m.Content));
            var estimatedTokens = TokenEstimator.EstimateTokens(
                userMessage.Content + (presetContent ?? string.Empty) + historyContent
            );

            var budget = await llmCallGuard.CheckTokenBudgetAsync(
                userId,
                estimatedTokens,
                cts.Token
            );
            if (!budget.IsSuccess)
            {
                await Clients
                    .Group(SessionGroup(sessionId))
                    .StreamError(assistantMessageId, budget.Error.Code, budget.Error.Message);
                return;
            }

            var routeResult = await modelRouter.ResolveAsync(
                feature,
                userId,
                session.ProjectId,
                estimatedTokens,
                cts.Token,
                preferredProviderModelConfigId
            );
            if (!routeResult.IsSuccess)
            {
                await Clients
                    .Group(SessionGroup(sessionId))
                    .StreamError(
                        assistantMessageId,
                        routeResult.Error.Code,
                        routeResult.Error.Message
                    );
                return;
            }

            var route = routeResult.Value;
            var client = llmClientFactory.For(route.Provider!);

            var compressedContext = await contextCompressor.CompressAsync(
                ragBlocks,
                route.Primary.MaxTokens ?? 4096,
                cts.Token
            );
            var cacheBlocks = compressedContext.IsSuccess
                ? compressedContext.Value.Blocks
                : ragBlocks;

            var request = new ChatRequest(
                route.Primary.Id,
                route.Primary.ModelId,
                chatMessages,
                cacheBlocks,
                [],
                4096,
                0.7m
            );

            await Clients
                .Group(SessionGroup(sessionId))
                .StreamStart(assistantMessageId, route.Primary.ModelId, route.Primary.Name);

            string content = string.Empty;
            ChatChunk? lastChunk = null;

            try
            {
                await foreach (var chunk in client.StreamChatAsync(request, cts.Token))
                {
                    lastChunk = chunk;

                    if (
                        chunk.FinishReason == ChatChunkFinishReason.Error
                        || chunk.FinishReason == ChatChunkFinishReason.ContentFilter
                    )
                    {
                        await Clients
                            .Group(SessionGroup(sessionId))
                            .StreamError(
                                assistantMessageId,
                                "STREAM_ERROR",
                                chunk.Delta ?? "Stream error"
                            );
                        return;
                    }

                    if (chunk.Delta is not null)
                    {
                        content += chunk.Delta;
                        await Clients
                            .Group(SessionGroup(sessionId))
                            .StreamDelta(assistantMessageId, chunk.Delta);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await Clients
                    .Group(SessionGroup(sessionId))
                    .StreamDone(assistantMessageId, null, null, null, null);
                return;
            }

            var assistantMessage = new Domain.Entities.Chat.ChatMessage
            {
                Id = assistantMessageId,
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = content,
                InputTokens = lastChunk?.Usage?.InputTokens ?? 0,
                OutputTokens = lastChunk?.Usage?.OutputTokens ?? 0,
                CachedTokens = lastChunk?.Usage?.CachedTokens ?? 0,
                ModelName = route.Primary.Name,
                CreatedAt = DateTime.UtcNow,
            };
            await messageRepo.AddAsync(assistantMessage);

            if (isFirstMessage)
            {
                try
                {
                    var titleResult = await titleGenerator.GenerateTitleAsync(
                        userId,
                        userMessage.Content,
                        content,
                        cts.Token
                    );
                    if (titleResult.IsSuccess)
                    {
                        session.UpdateSettings(titleResult.Value, null, null, null, null);
                        await sessionRepo.UpdateAsync(session, cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Chat title generation failed for session {SessionId}", sessionId);
                }
            }

            var recordInput = new TokenUsageRecordInput(
                UserId: userId,
                ProjectId: session.ProjectId,
                Feature: feature,
                ProviderModelConfigId: route.Primary.Id,
                ProviderId: route.Provider!.Id,
                ModelId: route.Primary.ModelId,
                ModelName: route.Primary.Name,
                InputTokens: lastChunk?.Usage?.InputTokens ?? 0,
                OutputTokens: lastChunk?.Usage?.OutputTokens ?? 0,
                CachedTokens: lastChunk?.Usage?.CachedTokens ?? 0,
                PipelineRunId: null,
                Cost: 0
            );
            await usageRecorder.RecordTokenAsync(recordInput, cts.Token);
            await usageRecorder.AccrueTokenUsageAsync(
                userId,
                (lastChunk?.Usage?.InputTokens ?? 0) + (lastChunk?.Usage?.OutputTokens ?? 0),
                cts.Token
            );

            await Clients
                .Group(SessionGroup(sessionId))
                .StreamDone(
                    assistantMessageId,
                    lastChunk?.Usage?.InputTokens,
                    lastChunk?.Usage?.OutputTokens,
                    lastChunk?.Usage?.CachedTokens,
                    route.Primary.Name
                );
        }
        catch (OperationCanceledException)
        {
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamDone(assistantMessageId, null, null, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SendMessage failed for session {SessionId}", sessionId);
            await Clients
                .Group(SessionGroup(sessionId))
                .StreamError(
                    assistantMessageId,
                    "INTERNAL_ERROR",
                    "An internal error occurred while processing your message. Please try again."
                );
        }
        finally
        {
            ActiveStreams.TryRemove(sessionId, out _);
            cts.Dispose();
        }
    }

    public async Task CancelStream(Guid sessionId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null || !await HasSessionAccessAsync(session, userId))
        {
            return;
        }

        if (ActiveStreams.TryGetValue(sessionId, out var ctx))
        {
            ctx.Cts.Cancel();
        }
    }
}
