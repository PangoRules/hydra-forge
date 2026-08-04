namespace HydraForge.Infrastructure.Housekeeping;

using HydraForge.Application.Housekeeping;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Hard-deletes rows past their retention cutoff. Most entities in
/// docs/archive/specs/2026-06-03-archive-and-housekeeping-design.md §3.1 are independent —
/// `WHERE ArchivedAt &lt; cutoff` is the whole story, and the four DB-level cascades
/// (Document→DocumentVersion, Note→NoteReminder, Note→NoteImageAttachment,
/// ChatSession→ChatMessage) take care of their children automatically.
///
/// Three relationships have no DB-level FK at all and would leave dangling references if
/// left to their own independent pass: a hard-deleted Project's Cards (Card.ProjectId has no
/// FK — and Column *does* cascade from Project, so an orphaned Card would point at both a
/// dead ColumnId and a dead ProjectId), a hard-deleted Card's Comment/ChecklistItem/
/// Attachment/CardAssignee/CardWatcher/CardRelationship/CardChatLink rows, a hard-deleted
/// GalleryImage's ImageTag rows, and a hard-deleted Album's AlbumImage rows. Those four are
/// force-cleaned regardless of the child's own ArchivedAt before the parent row is removed.
/// Everything else (e.g. a Card's nullable ParentCardId, a CalendarEvent's CalendarSourceId)
/// is left as an acceptable dangling reference for the MVP, same call the original design
/// made for orphaned file blobs.
///
/// Deletes go through tracked <c>RemoveRange</c> + one final <c>SaveChangesAsync</c> rather
/// than <c>ExecuteDeleteAsync</c> — this is a once-daily batch job, not a hot path, and the
/// tracked form is what the project's InMemory-backed Infrastructure test fixtures can
/// exercise (<c>ExecuteDeleteAsync</c> isn't supported by the InMemory provider). One
/// SaveChangesAsync call also makes the whole run a single DB transaction.
/// </summary>
public sealed class EfHousekeepingRepository(HydraForgeDbContext db) : IHousekeepingRepository
{
    public async Task<HousekeepingRunResult> RunAsync(
        HousekeepingCutoffs cutoffs,
        CancellationToken ct = default
    )
    {
        var filePaths = new List<string>();
        var itemCutoff = cutoffs.ArchivedItemCutoff;

        // --- Projects (forces their Cards out too — Column cascades from Project via FK,
        // but Card does not cascade from Column, so an un-forced Card would dangle). ---
        var projectsToDelete = await db
            .Projects.Where(p => p.ArchivedAt != null && p.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        var projectIdsToDelete = projectsToDelete.Select(p => p.Id).ToList();

        var independentCards = await db
            .Cards.Where(c => c.ArchivedAt != null && c.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        var projectCards =
            projectIdsToDelete.Count == 0
                ? []
                : await db
                    .Cards.Where(c => projectIdsToDelete.Contains(c.ProjectId))
                    .ToListAsync(ct);
        var cardsToDelete = independentCards.Concat(projectCards).DistinctBy(c => c.Id).ToList();
        var cardIdsToDelete = cardsToDelete.Select(c => c.Id).ToList();

        var forcedComments = 0;
        var forcedRelationships = 0;
        var forcedCardChatLinks = 0;
        if (cardIdsToDelete.Count > 0)
        {
            var attachmentsToDelete = await db
                .Attachments.Where(a => cardIdsToDelete.Contains(a.CardId))
                .ToListAsync(ct);
            filePaths.AddRange(attachmentsToDelete.Select(a => a.StoragePath));
            db.Attachments.RemoveRange(attachmentsToDelete);

            var checklistItems = await db
                .ChecklistItems.Where(x => cardIdsToDelete.Contains(x.CardId))
                .ToListAsync(ct);
            db.ChecklistItems.RemoveRange(checklistItems);

            var cardAssignees = await db
                .CardAssignees.Where(x => cardIdsToDelete.Contains(x.CardId))
                .ToListAsync(ct);
            db.CardAssignees.RemoveRange(cardAssignees);

            var cardWatchers = await db
                .CardWatchers.Where(x => cardIdsToDelete.Contains(x.CardId))
                .ToListAsync(ct);
            db.CardWatchers.RemoveRange(cardWatchers);

            var forcedRelationshipRows = await db
                .CardRelationships.Where(x =>
                    cardIdsToDelete.Contains(x.SourceCardId)
                    || cardIdsToDelete.Contains(x.TargetCardId)
                )
                .ToListAsync(ct);
            forcedRelationships = forcedRelationshipRows.Count;
            db.CardRelationships.RemoveRange(forcedRelationshipRows);

            var forcedCardChatLinkRows = await db
                .CardChatLinks.Where(x => cardIdsToDelete.Contains(x.CardId))
                .ToListAsync(ct);
            forcedCardChatLinks = forcedCardChatLinkRows.Count;
            db.CardChatLinks.RemoveRange(forcedCardChatLinkRows);

            var forcedCommentRows = await db
                .Comments.Where(x => cardIdsToDelete.Contains(x.CardId))
                .ToListAsync(ct);
            forcedComments = forcedCommentRows.Count;
            db.Comments.RemoveRange(forcedCommentRows);

            // Spec, SpecVersion, Plan, PlanVersion cascade from Card automatically via FK.
        }
        db.Cards.RemoveRange(cardsToDelete);

        // Columns cascade from Project via FK automatically.
        db.Projects.RemoveRange(projectsToDelete);

        // Independent passes for Comment/CardRelationship/CardChatLink whose own Card isn't
        // being deleted (e.g. one comment archived on an otherwise-active card).
        var independentComments = await db
            .Comments.Where(c => c.ArchivedAt != null && c.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.Comments.RemoveRange(independentComments);

        var independentRelationships = await db
            .CardRelationships.Where(r => r.ArchivedAt != null && r.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.CardRelationships.RemoveRange(independentRelationships);

        var independentCardChatLinks = await db
            .CardChatLinks.Where(l => l.ArchivedAt != null && l.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.CardChatLinks.RemoveRange(independentCardChatLinks);

        // --- Chat (ChatMessage/ChatSessionDocument cascade from ChatSession via FK). ---
        var chatSessions = await db
            .ChatSessions.Where(s => s.ArchivedAt != null && s.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.ChatSessions.RemoveRange(chatSessions);

        var chatFolders = await db
            .ChatFolders.Where(f => f.ArchivedAt != null && f.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.ChatFolders.RemoveRange(chatFolders);

        var agentPersonalities = await db
            .AgentPersonalities.Where(p => p.ArchivedAt != null && p.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.AgentPersonalities.RemoveRange(agentPersonalities);

        // PromptPreset cascades from PromptPresetGroup via FK — run the independent preset
        // pass first so a preset archived on its own is still counted individually.
        var promptPresets = await db
            .PromptPresets.Where(p => p.ArchivedAt != null && p.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.PromptPresets.RemoveRange(promptPresets);

        var promptPresetGroups = await db
            .PromptPresetGroups.Where(g => g.ArchivedAt != null && g.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.PromptPresetGroups.RemoveRange(promptPresetGroups);

        // --- Documents (DocumentVersion cascades via FK; DocumentChunk has no FK). ---
        var documentsToDelete = await db
            .Documents.Where(d => d.ArchivedAt != null && d.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        if (documentsToDelete.Count > 0)
        {
            var documentIds = documentsToDelete.Select(d => d.Id).ToList();
            var chunksToDelete = await db
                .DocumentChunks.Where(c =>
                    c.SourceType == "document" && documentIds.Contains(c.SourceId)
                )
                .ToListAsync(ct);
            db.DocumentChunks.RemoveRange(chunksToDelete);
            filePaths.AddRange(documentsToDelete.Select(d => d.FilePath).OfType<string>());
        }
        db.Documents.RemoveRange(documentsToDelete);

        // --- Notes (NoteReminder/NoteImageAttachment cascade via FK — capture image paths
        // before they're removed). ---
        var notesToDelete = await db
            .Notes.Where(n => n.ArchivedAt != null && n.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        if (notesToDelete.Count > 0)
        {
            var noteIds = notesToDelete.Select(n => n.Id).ToList();
            var noteImagePaths = await db
                .NoteImageAttachments.Where(a => noteIds.Contains(a.NoteId))
                .Select(a => a.FilePath)
                .ToListAsync(ct);
            filePaths.AddRange(noteImagePaths);
        }
        db.Notes.RemoveRange(notesToDelete);

        // --- Gallery (ImageTag has no FK — force-clean before removing the image). ---
        var galleryImagesToDelete = await db
            .GalleryImages.Where(g => g.ArchivedAt != null && g.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        if (galleryImagesToDelete.Count > 0)
        {
            var imageIds = galleryImagesToDelete.Select(g => g.Id).ToList();
            var imageTagsToDelete = await db
                .ImageTags.Where(t => imageIds.Contains(t.ImageId))
                .ToListAsync(ct);
            db.ImageTags.RemoveRange(imageTagsToDelete);
            filePaths.AddRange(galleryImagesToDelete.Select(g => g.FilePath));
            filePaths.AddRange(galleryImagesToDelete.Select(g => g.ThumbnailPath).OfType<string>());
        }
        db.GalleryImages.RemoveRange(galleryImagesToDelete);

        // --- Albums (AlbumImage has no FK — force-clean before removing the album, then an
        // independent pass for album images archived on their own). ---
        var albumsToDelete = await db
            .Albums.Where(a => a.ArchivedAt != null && a.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        if (albumsToDelete.Count > 0)
        {
            var albumIds = albumsToDelete.Select(a => a.Id).ToList();
            var forcedAlbumImages = await db
                .AlbumImages.Where(i => albumIds.Contains(i.AlbumId))
                .ToListAsync(ct);
            db.AlbumImages.RemoveRange(forcedAlbumImages);
        }
        db.Albums.RemoveRange(albumsToDelete);

        var independentAlbumImages = await db
            .AlbumImages.Where(i => i.ArchivedAt != null && i.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.AlbumImages.RemoveRange(independentAlbumImages);

        // --- Remaining simple, self-contained entities. ---
        var memoryEntries = await db
            .MemoryEntries.Where(m => m.ArchivedAt != null && m.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.MemoryEntries.RemoveRange(memoryEntries);

        var calendarEvents = await db
            .CalendarEvents.Where(e => e.ArchivedAt != null && e.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.CalendarEvents.RemoveRange(calendarEvents);

        var calendarSources = await db
            .CalendarSources.Where(s => s.ArchivedAt != null && s.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.CalendarSources.RemoveRange(calendarSources);

        var personalTasks = await db
            .PersonalTasks.Where(t => t.ArchivedAt != null && t.ArchivedAt < itemCutoff)
            .ToListAsync(ct);
        db.PersonalTasks.RemoveRange(personalTasks);

        // --- Operational / transient, age-based (not ArchivedAt-gated). ---
        var notifications = await db
            .Notifications.Where(n => n.IsRead && n.CreatedAt < cutoffs.NotificationCutoff)
            .ToListAsync(ct);
        db.Notifications.RemoveRange(notifications);

        var auditLogEntries = await db
            .AuditLogEntries.Where(a => a.Timestamp < cutoffs.AuditLogCutoff)
            .ToListAsync(ct);
        db.AuditLogEntries.RemoveRange(auditLogEntries);

        var tokenUsageRecords = await db
            .TokenUsageRecords.Where(r => r.CreatedAt < cutoffs.AuditLogCutoff)
            .ToListAsync(ct);
        db.TokenUsageRecords.RemoveRange(tokenUsageRecords);

        var imageUsageRecords = await db
            .ImageUsageRecords.Where(r => r.CreatedAt < cutoffs.AuditLogCutoff)
            .ToListAsync(ct);
        db.ImageUsageRecords.RemoveRange(imageUsageRecords);

        await db.SaveChangesAsync(ct);

        var counts = new HousekeepingCounts(
            ChatSessions: chatSessions.Count,
            ChatFolders: chatFolders.Count,
            AgentPersonalities: agentPersonalities.Count,
            PromptPresets: promptPresets.Count,
            PromptPresetGroups: promptPresetGroups.Count,
            Documents: documentsToDelete.Count,
            Notes: notesToDelete.Count,
            GalleryImages: galleryImagesToDelete.Count,
            Albums: albumsToDelete.Count,
            AlbumImages: independentAlbumImages.Count,
            MemoryEntries: memoryEntries.Count,
            CalendarEvents: calendarEvents.Count,
            CalendarSources: calendarSources.Count,
            PersonalTasks: personalTasks.Count,
            CardChatLinks: forcedCardChatLinks + independentCardChatLinks.Count,
            CardRelationships: forcedRelationships + independentRelationships.Count,
            Comments: forcedComments + independentComments.Count,
            Cards: cardsToDelete.Count,
            Projects: projectsToDelete.Count,
            Notifications: notifications.Count,
            AuditLogEntries: auditLogEntries.Count,
            TokenUsageRecords: tokenUsageRecords.Count,
            ImageUsageRecords: imageUsageRecords.Count
        );

        return new HousekeepingRunResult(counts, filePaths);
    }
}
