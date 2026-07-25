namespace HydraForge.Domain.Entities.PersonalSpace;

public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public Guid? CardId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? ActionUrl { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        string title,
        string? body,
        string message,
        Guid? cardId,
        Guid? projectId,
        string? actionUrl)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            Message = message,
            CardId = cardId,
            ProjectId = projectId,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void MarkRead()
    {
        IsRead = true;
    }
}
