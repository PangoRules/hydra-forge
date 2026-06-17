using HydraForge.Domain.Enums;

namespace HydraForge.Application.Attachments;

public record CreateAttachmentCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    string FileName,
    string ContentType,
    long Size,
    Stream Content
);

public record AttachmentDto(
    Guid Id,
    Guid CardId,
    string FileName,
    string ContentType,
    long Size,
    DateTime CreatedAt
);

public record AttachmentListFilter();

public enum AttachmentStorageProvider
{
    Local,
    S3
}

public static class AttachmentContentTypes
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/pdf",
        "text/plain",
        "application/json",
        "application/xml",
        "text/html",
        "text/csv",
        "application/zip",
        "application/x-zip-compressed",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };
}