using System.Text.RegularExpressions;
using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Attachments;

public partial class AttachmentService(
    IAttachmentRepository attachmentRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IFileStore fileStore,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    long maxBytes,
    IReadOnlySet<string> allowedContentTypes
)
{
    private readonly IAttachmentRepository _attachmentRepo = attachmentRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IFileStore _fileStore = fileStore;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly long _maxBytes = maxBytes;
    private readonly IReadOnlySet<string> _allowedContentTypes = allowedContentTypes;

    public async Task<Result<AttachmentDto>> CreateAsync(CreateAttachmentCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<AttachmentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<AttachmentDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (cmd.Size > _maxBytes)
            return Result<AttachmentDto>.Failure(
                new Error(DomainErrorCodes.Attachments.FileTooLarge, $"File exceeds maximum size of {_maxBytes} bytes.")
            );

        if (!_allowedContentTypes.Contains(cmd.ContentType))
            return Result<AttachmentDto>.Failure(
                new Error(DomainErrorCodes.Attachments.UnsupportedContentType, $"Content type '{cmd.ContentType}' is not allowed.")
            );

        var sanitizedFileName = SanitizeFileName(cmd.FileName);
        var storageKey = GenerateStorageKey(cmd.ActorId, cmd.CardId);

        var storeResult = await _fileStore.StoreAsync(cmd.Content, cmd.ContentType, storageKey, ct);
        if (storeResult.IsFailure)
            return Result<AttachmentDto>.Failure(
                new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable.")
            );

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            CardId = cmd.CardId,
            FileName = sanitizedFileName,
            ContentType = cmd.ContentType,
            Size = cmd.Size,
            StoragePath = storeResult.Value,
            UploadedByUserId = cmd.ActorId,
            CreatedAt = DateTime.UtcNow,
        };

        await _attachmentRepo.AddAsync(attachment, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Attachment",
                attachment.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<AttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<AttachmentDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<IReadOnlyList<AttachmentDto>>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var attachments = await _attachmentRepo.ListByCardAsync(cardId, ct);
        return Result<IReadOnlyList<AttachmentDto>>.Success(
            attachments.Select(MapToDto).ToList()
        );
    }

    public async Task<Result<(Stream Stream, string ContentType, string FileName)>> DownloadAsync(
        Guid projectId,
        Guid cardId,
        Guid attachmentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<(Stream, string, string)>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<(Stream, string, string)>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var attachment = await _attachmentRepo.GetByIdAsync(attachmentId, ct);
        if (attachment == null || attachment.CardId != cardId)
            return Result<(Stream, string, string)>.Failure(
                new Error(DomainErrorCodes.Attachments.NotFound, "Attachment not found.")
            );

        var openResult = await _fileStore.OpenReadAsync(attachment.StoragePath, ct);
        if (openResult.IsFailure)
            return Result<(Stream, string, string)>.Failure(
                new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable.")
            );

        return Result<(Stream, string, string)>.Success((openResult.Value, attachment.ContentType, attachment.FileName));
    }

    public async Task<Result> DeleteAsync(
        Guid projectId,
        Guid cardId,
        Guid attachmentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var attachment = await _attachmentRepo.GetByIdAsync(attachmentId, ct);
        if (attachment == null || attachment.CardId != cardId)
            return Result.Failure(
                new Error(DomainErrorCodes.Attachments.NotFound, "Attachment not found.")
            );

        await _attachmentRepo.DeleteAsync(attachmentId, ct);
        await _snapshotRefresher.RefreshAsync(projectId, ct);

        try
        {
            await _fileStore.DeleteAsync(attachment.StoragePath, ct);
        }
        catch
        {
            // File deletion failure is non-fatal — metadata already removed
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                actorId,
                AuditLogScope.Project,
                "Attachment",
                attachmentId,
                "Deleted",
                projectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        var name = Path.GetFileName(fileName);
        name = DangerousPathCharsRegex().Replace(name, "_");
        name = TrimWithMaxLength(name, 255);
        return string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
    }

    private static string GenerateStorageKey(Guid userId, Guid cardId)
    {
        return $"{userId}/cards/{cardId}/{Guid.NewGuid()}";
    }

    private static string TrimWithMaxLength(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;
        var ext = Path.GetExtension(value);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(value);
        var availableForName = maxLength - ext.Length;
        return availableForName > 0
            ? nameWithoutExt[..availableForName] + ext
            : value[..maxLength];
    }

    private static AttachmentDto MapToDto(Attachment a) =>
        new(a.Id, a.CardId, a.FileName, a.ContentType, a.Size, a.CreatedAt);

    [GeneratedRegex(@"[\\/:*?""<>|]")]
    private static partial Regex DangerousPathCharsRegex();
}