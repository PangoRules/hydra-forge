using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.Chat;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? FolderId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public ChatSessionStatus Status { get; set; } = ChatSessionStatus.Active;
    public AiEditMode AiEditMode { get; set; } = AiEditMode.PerMutation;
    public bool SearchAllMyDocs { get; set; }
    public Guid? PersonalityId { get; set; }
    public Guid? OpenCardId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Summary { get; set; }

    public void Open(Guid? personalityId, Guid? openCardId, AiEditMode aiEditMode)
    {
        Status = ChatSessionStatus.Active;
        PersonalityId = personalityId;
        OpenCardId = openCardId;
        AiEditMode = aiEditMode;
        ClosedAt = null;
        Summary = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close(string? summary)
    {
        if (Status == ChatSessionStatus.Closed)
            return;

        Status = ChatSessionStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        Summary = summary;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ToggleSearchAllMyDocs(bool value)
    {
        if (Status != ChatSessionStatus.Active)
            throw new InvalidOperationException("Cannot toggle search flag on a closed session.");

        SearchAllMyDocs = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAiEditMode(AiEditMode mode)
    {
        if (Status != ChatSessionStatus.Active)
            throw new InvalidOperationException("Cannot change AI edit mode on a closed session.");

        AiEditMode = mode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
