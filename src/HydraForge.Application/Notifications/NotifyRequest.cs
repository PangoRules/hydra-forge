namespace HydraForge.Application.Notifications;

public record NotifyRequest(
    Guid UserId,
    Guid ActorId,
    string Title,
    string? Body,
    string? Message,
    Guid? CardId,
    Guid? ProjectId,
    string? ActionUrl
);
