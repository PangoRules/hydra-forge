namespace HydraForge.Application.Housekeeping;

public record HousekeepingCutoffs(
    DateTime ArchivedItemCutoff,
    DateTime AuditLogCutoff,
    DateTime NotificationCutoff
);

public record HousekeepingCounts(
    int ChatSessions,
    int ChatFolders,
    int AgentPersonalities,
    int PromptPresets,
    int PromptPresetGroups,
    int Documents,
    int Notes,
    int GalleryImages,
    int Albums,
    int AlbumImages,
    int MemoryEntries,
    int CalendarEvents,
    int CalendarSources,
    int PersonalTasks,
    int CardChatLinks,
    int CardRelationships,
    int Comments,
    int Cards,
    int Projects,
    int Notifications,
    int AuditLogEntries,
    int TokenUsageRecords,
    int ImageUsageRecords
)
{
    public int Total =>
        ChatSessions
        + ChatFolders
        + AgentPersonalities
        + PromptPresets
        + PromptPresetGroups
        + Documents
        + Notes
        + GalleryImages
        + Albums
        + AlbumImages
        + MemoryEntries
        + CalendarEvents
        + CalendarSources
        + PersonalTasks
        + CardChatLinks
        + CardRelationships
        + Comments
        + Cards
        + Projects
        + Notifications
        + AuditLogEntries
        + TokenUsageRecords
        + ImageUsageRecords;
}

public record HousekeepingRunResult(
    HousekeepingCounts Counts,
    IReadOnlyList<string> FilePathsToDelete
);
