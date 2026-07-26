namespace HydraForge.Application.Realtime;

public record NotificationReceivedEvent(
    Guid Id,
    string Title,
    string? Body,
    Guid? CardId,
    Guid? ProjectId,
    string? ActionUrl,
    DateTime CreatedAt,
    bool IsRead
);
