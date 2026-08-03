using System.Collections.Concurrent;
using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Constants;
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
    ILogger<ChatHub> logger,
    IOptions<LlmOptions> llmOptions)
    : Hub<IChatHub>
{
    private static readonly ConcurrentDictionary<Guid, StreamContext> ActiveStreams = new();

    private static string SessionGroup(Guid sessionId) => $"chat-{sessionId}";

    private record StreamContext(CancellationTokenSource Cts, Guid MessageId);

    public async Task JoinSession(Guid sessionId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            throw new HubException("CHAT_SESSION_NOT_FOUND");

        if (session.ProjectId.HasValue)
        {
            var isAdmin = Context.User!.IsInRole(Roles.Admin);
            if (!isAdmin)
            {
                var membership = await memberRepo.GetByProjectAndUserAsync(session.ProjectId.Value, userId);
                if (membership is null)
                    throw new HubException("Access denied");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task SendMessage(Guid sessionId, Guid userMessageId, Guid? presetId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var userMessage = await messageRepo.GetByIdAsync(userMessageId);
        if (userMessage is null || userMessage.SessionId != sessionId || userMessage.Role != MessageRole.User)
        {
            await Clients.Caller.StreamError(userMessageId, "CHAT_MESSAGE_NOT_FOUND", "Message not found");
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            await Clients.Caller.StreamError(userMessageId, "CHAT_SESSION_NOT_FOUND", "Session not found");
            return;
        }

        if (session.Status != ChatSessionStatus.Active)
        {
            await Clients.Caller.StreamError(userMessageId, "CHAT_SESSION_CLOSED", "Session is closed");
            return;
        }

        if (session.ArchivedAt.HasValue)
        {
            await Clients.Caller.StreamError(userMessageId, "CHAT_SESSION_ARCHIVED", "Session has been archived");
            return;
        }

        if (!ActiveStreams.TryAdd(sessionId, new StreamContext(default!, default)))
        {
            await Clients.Caller.StreamError(userMessageId, "CHAT_STREAM_IN_PROGRESS", "A stream is already active for this session");
            return;
        }

        var budget = await llmCallGuard.CheckTokenBudgetAsync(userId, 0);
        if (!budget.IsSuccess)
        {
            ActiveStreams.TryRemove(sessionId, out _);
            await Clients.Caller.StreamError(userMessageId, budget.Error.Code, budget.Error.Message);
            return;
        }

        var assistantMessageId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        ActiveStreams[sessionId] = new StreamContext(cts, assistantMessageId);

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
                    cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RAG retrieval failed for session {SessionId}, proceeding without context", sessionId);
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

            foreach (var block in ragBlocks)
            {
                chatMessages.Add(new ChatMessage(ChatRole.System, block.Content));
            }

            var history = await messageRepo.GetBySessionAsync(sessionId, null, null, 100);
            foreach (var msg in history)
            {
                var role = msg.Role switch
                {
                    MessageRole.User => ChatRole.User,
                    MessageRole.Assistant => ChatRole.Assistant,
                    MessageRole.System => ChatRole.System,
                    MessageRole.Tool => ChatRole.Tool,
                    _ => ChatRole.User
                };

                var images = ChatMessageMapper.ToApplicationImages(msg.ImagesJson);
                chatMessages.Add(new ChatMessage(role, msg.Content, images.Length > 0 ? images : null));
            }

            var feature = session.ProjectId.HasValue ? AiFeature.ProjectChat : AiFeature.PersonalChat;
            var routeResult = await modelRouter.ResolveAsync(feature, userId, session.ProjectId, 0, cts.Token);
            if (!routeResult.IsSuccess)
            {
                await Clients.Caller.StreamError(assistantMessageId, routeResult.Error.Code, routeResult.Error.Message);
                return;
            }

            var route = routeResult.Value;
            var client = llmClientFactory.For(route.Provider);

            var request = new ChatRequest(
                route.Primary.Id,
                route.Primary.ModelId,
                chatMessages,
                [],
                [],
                4096,
                0.7m
            );

            await Clients.Caller.StreamStart(assistantMessageId, route.Primary.ModelId, route.Primary.Name);

            string content = string.Empty;
            ChatChunk? lastChunk = null;

            try
            {
                await foreach (var chunk in client.StreamChatAsync(request, cts.Token))
                {
                    lastChunk = chunk;

                    if (chunk.FinishReason == ChatChunkFinishReason.Error || chunk.FinishReason == ChatChunkFinishReason.ContentFilter)
                    {
                        await Clients.Caller.StreamError(assistantMessageId, "STREAM_ERROR", chunk.Delta ?? "Stream error");
                        return;
                    }

                    if (chunk.Delta is not null)
                    {
                        content += chunk.Delta;
                        await Clients.Caller.StreamDelta(assistantMessageId, chunk.Delta);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.StreamDone(assistantMessageId, null, null, null, null);
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
                CreatedAt = DateTime.UtcNow
            };
            await messageRepo.AddAsync(assistantMessage);

            var recordInput = new TokenUsageRecordInput(
                UserId: userId,
                ProjectId: session.ProjectId,
                Feature: feature,
                ProviderModelConfigId: route.Primary.Id,
                ProviderId: route.Provider.Id,
                ModelId: route.Primary.ModelId,
                ModelName: route.Primary.Name,
                InputTokens: lastChunk?.Usage?.InputTokens ?? 0,
                OutputTokens: lastChunk?.Usage?.OutputTokens ?? 0,
                CachedTokens: lastChunk?.Usage?.CachedTokens ?? 0,
                PipelineRunId: null,
                Cost: 0
            );
            await usageRecorder.RecordTokenAsync(recordInput, cts.Token);
            await llmCallGuard.AccrueAfterCallAsync(userId, lastChunk?.Usage, cts.Token);

            await Clients.Caller.StreamDone(
                assistantMessageId,
                lastChunk?.Usage?.InputTokens,
                lastChunk?.Usage?.OutputTokens,
                lastChunk?.Usage?.CachedTokens,
                route.Primary.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SendMessage failed for session {SessionId}", sessionId);
            await Clients.Caller.StreamError(assistantMessageId, "INTERNAL_ERROR", ex.Message);
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

        if (ActiveStreams.TryGetValue(sessionId, out var ctx))
        {
            ctx.Cts.Cancel();
            await Clients.Caller.StreamDone(ctx.MessageId, null, null, null, null);
            ActiveStreams.TryRemove(sessionId, out _);
        }
    }
}
