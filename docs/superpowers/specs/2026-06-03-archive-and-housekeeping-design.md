# Archive & Housekeeping Policy — Design

> **Date:** 2026-06-03
> **Status:** Design approved, awaiting user spec review before implementation
> **Scope:** Phase 1 Foundation — schema additions, replacement of `IsArchived: bool` with `ArchivedAt: DateTime?`, system settings table, housekeeping background service, cascading-archive application services

---

## 1. Goals

- One admin-configurable retention period for all archived user-facing content.
- Archived items remain visible to their owner after they archive them.
- A scheduled housekeeping job hard-deletes archived items older than the retention period.
- Uniform pattern across all "ownable" entities — no per-entity bespoke fields or behavior.
- Existing `ArchivedAt?` pattern on `ChatSession`, `ChatFolder`, `AgentPersonality`, `GalleryImage`, `Album`, `AlbumImage` is preserved and extended.

## 2. Non-Goals

- Offline mode, local state, conflict resolution (NFR-4 / architecture decisions).
- Per-domain retention overrides. (One global value; per-domain can come later if needed.)
- Restore UI / flow. (Archived items are visible; unarchive is a future feature, not blocked by this design.)
- Cross-instance coordination of housekeeping. (Single-server assumption for MVP.)
- Notification `ArchivedAt` / user-facing notification archive. (Notifications use age-based cleanup with `IsRead`.)
- `DocumentChunk` independent archive. (Derived rows; cascade from source.)

## 3. Entity Classification

### 3.1 Ownable / user-facing content (gets `ArchivedAt: DateTime?`)

| Entity | Current state | Action |
|---|---|---|
| `GalleryImage` | has `ArchivedAt?` | done |
| `Album` | has `ArchivedAt?` | done |
| `AlbumImage` | has `ArchivedAt?` | done |
| `AgentPersonality` | has `ArchivedAt?` | done |
| `ChatSession` | has `ArchivedAt?` | done |
| `ChatFolder` | has `ArchivedAt?` | done |
| `Card` | none | **add `ArchivedAt?`** |
| `Document` | has `IsArchived: bool` | **replace with `ArchivedAt?`** |
| `Note` | has `IsArchived: bool` | **replace with `ArchivedAt?`** |
| `MemoryEntry` | none | **add `ArchivedAt?`** |
| `CalendarEvent` | none | **add `ArchivedAt?`** |
| `CalendarSource` | none | **add `ArchivedAt?`** |
| `PersonalTask` | none | **add `ArchivedAt?`** |
| `CardChatLink` | none | **add `ArchivedAt?`** |

### 3.2 Child / derived rows (no independent `ArchivedAt`; lifecycle follows parent)

| Entity | Parent | Cascade behavior |
|---|---|---|
| `ChatMessage` | `ChatSession` | Housekeeping deletes when `ChatSession` is hard-deleted |
| `DocumentVersion` | `Document` | Housekeeping deletes with `Document`; per-version pruning by count/age is a future feature |
| `DocumentChunk` | `Document` / `Note` / `MemoryEntry` (`SourceType`+`SourceId`) | Housekeeping deletes with source. Regeneration logic unaffected. |
| `NoteImageAttachment` | `Note` | Housekeeping deletes with `Note`; also delete the underlying file blob |
| `NoteReminder` | `Note` | One-shot: hard-delete 30 days after `IsSent=true`. Recurring: disable (set `IsSent=true`, clear `RepeatPattern`); delete with `Note`. |

### 3.3 Operational / transient (age-based, no `ArchivedAt`)

| Entity | Rule |
|---|---|
| `Notification` | Hard-delete when `IsRead=true` AND `CreatedAt < UtcNow - NotificationRetentionDays` |
| `AuditLogEntry` | Hard-delete when `Timestamp < UtcNow - AuditLogRetentionDays` (satisfies NFR-7) |
| `TokenUsageRecord` | Hard-delete when `CreatedAt < UtcNow - AuditLogRetentionDays` (NFR-7) |
| `ImageUsageRecord` | Hard-delete when `CreatedAt < UtcNow - AuditLogRetentionDays` (NFR-7) |

## 4. Data Model Changes

### 4.1 Entity field changes

```csharp
// Card
public DateTime? ArchivedAt { get; set; }

// Document (replaces IsArchived)
public DateTime? ArchivedAt { get; set; }

// Note (replaces IsArchived)
public DateTime? ArchivedAt { get; set; }

// MemoryEntry
public DateTime? ArchivedAt { get; set; }

// CalendarEvent
public DateTime? ArchivedAt { get; set; }

// CalendarSource
public DateTime? ArchivedAt { get; set; }

// PersonalTask
public DateTime? ArchivedAt { get; set; }

// CardChatLink
public DateTime? ArchivedAt { get; set; }
```

### 4.2 New entity: `SystemSettings` (singleton)

```csharp
public class SystemSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ArchivedItemRetentionDays { get; set; } = 730;  // 2 years
    public int AuditLogRetentionDays { get; set; } = 90;      // satisfies NFR-7
    public int NotificationRetentionDays { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

A single row is seeded on first run. Future admin UI edits the row directly; no schema change for new settings.

## 5. Application Services (cascading archive)

### 5.1 `ChatArchiveService.ArchiveFolder(folderId)`

Sets `ChatFolder.ArchivedAt = UtcNow` and sets `ChatSession.ArchivedAt = UtcNow` for every session in the folder. Used by `Project.Archive` (already FR-1) to cascade down.

### 5.2 `ProjectArchiveService.Archive(projectId)`

Sets `Project.ArchivedAt` (if/when added — out of scope for this design, but service exists for the future), `ChatFolder.ArchivedAt`, and cascades to `ChatSession.ArchivedAt` via `ChatArchiveService.ArchiveFolder`. No new schema for `Project` in this design.

### 5.3 `NoteArchiveService.Archive(noteId)`

Sets `Note.ArchivedAt`. Disables recurring `NoteReminder`s (`IsSent=true`, `RepeatPattern=null`). The note's `NoteImageAttachment` rows and `DocumentChunk` rows remain in DB until housekeeping runs.

## 6. Housekeeping Job

### 6.1 Service

`HousekeepingBackgroundService : BackgroundService`, registered in `Server` DI.

### 6.2 Schedule

- Default: **daily at 03:00 UTC**.
- Configurable via `SystemSettings` (future field) or `appsettings.json` initially.

### 6.3 Behavior

1. Read `SystemSettings` (cache refreshed every 5 min).
2. Compute cutoff: `UtcNow - ArchivedItemRetentionDays`.
3. For each entity in §3.1, batch-delete (size 1000) rows where `ArchivedAt < cutoff`.
4. Cascade-deletes happen via `OnDelete: Cascade` FK configuration in `DbContext`: deleting a `Document` triggers cascade for `DocumentVersion` and `DocumentChunk` rows; deleting a `Note` cascades to `NoteReminder` and `NoteImageAttachment`; deleting a `ChatSession` cascades to `ChatMessage`. (The implementation plan will add the FK cascade configuration as part of housekeeping infra.)
5. Notification cleanup: delete `Notification` where `IsRead=true` AND `CreatedAt < UtcNow - NotificationRetentionDays`.
6. Audit/usage cleanup: delete `AuditLogEntry`, `TokenUsageRecord`, `ImageUsageRecord` where created/timestamped < `UtcNow - AuditLogRetentionDays`.
7. Write an `AuditLogEntry` (action=`"Housekeeping run"`) with JSON `NewValue` = per-entity counts.
8. Idempotent: re-running within the same day is a no-op (nothing meets cutoff yet).

### 6.4 Filesystem side effects

When a `Document` (binary), `NoteImageAttachment`, or `GalleryImage` is hard-deleted, the underlying file blob should also be deleted from storage. This service does not define `IFileStorageService` — it is a separate concern. For this design, the housekeeping service logs the file path it needs to delete, and the file-cleanup step is a future implementation. The DB row deletion is the authoritative state; orphaned files are an acceptable temporary state for the MVP.

## 7. Tests

### 7.1 EF model contract tests (existing pattern, in `HydraForgeDbContextModelTests`)

For each entity in §3.1 not yet asserted, add `ArchivedAt` to the `AssertProperties` list. This catches drift between code and the classification.

### 7.2 Replacement tests for `IsArchived` removal

Assert that `Note.IsArchived` and `Document.IsArchived` no longer exist on the model; assert `ArchivedAt` is present.

### 7.3 `SystemSettings` test

Assert that the table is created with the three retention columns and seeded with one row of defaults.

### 7.4 `HousekeepingService` unit tests (in `Application.Tests`)

- Seed: one archived `Document` older than cutoff, one archived `Document` newer than cutoff, one active `Document`. Run service. Assert only the old archived row is deleted.
- Seed: archived `Note` with `NoteReminder` (recurring) and `NoteImageAttachment`. Run. Assert `Note` deleted, `NoteReminder` deleted, `NoteImageAttachment` deleted. (NoteArchiveService.Archive already disabled the recurring reminder at archive time; housekeeping hard-deletes the row.)
- Seed: `ChatSession` with `ChatMessage` rows, archived beyond cutoff. Run. Assert both deleted.
- Idempotency: run twice; second run is a no-op.
- Notification cleanup: read+old deleted; unread kept.
- Audit/usage retention: rows older than `AuditLogRetentionDays` deleted; recent kept.
- `SystemSettings` change: after updating `ArchivedItemRetentionDays = 0`, archived+recent rows are now eligible.

### 7.5 Migration tests

- Apply migration on empty DB; assert all expected columns exist and `IsArchived` columns are gone.
- No data backfill needed (foundation DB has no data).

## 8. Migration

Single `dotnet ef migrations add AddArchiveAndHousekeepingFoundation` after all entity changes are in. Includes:
- Add `ArchivedAt?` to all entities in §3.1
- Drop `IsArchived` from `Note` and `Document`
- Add `SystemSettings` table with seed row
- Default index on `ArchivedAt` for fast housekeeping queries (composite optional, see §9)

## 9. Open Questions / Future Work

- **Indexes**: should `ArchivedAt` be indexed? Housekeeping query is `WHERE ArchivedAt < cutoff` — partial index `WHERE ArchivedAt IS NOT NULL` would help. Out of scope for this design; add in a follow-up if performance warrants.
- **Per-domain retention**: deferred. Global only for MVP.
- **Restore UI**: out of scope. Archived items visible; unarchive is a future feature.
- **Settings UI**: out of scope. Admins edit `SystemSettings` via DB / future admin UI.
- **Cross-instance lock**: if HydraForge ever runs multi-instance, a `SELECT ... FOR UPDATE SKIP LOCKED` or distributed lock is needed to prevent two housekeepers running at once. Out of scope for MVP (single-server).
- **`Document.ContentType` → enum**: tracked separately as TODO on the entity. Not affected by this design.
- **Notification `IsRead` rename to `ReadAt: DateTime?`**: housekeeping needs the read timestamp to compute "read N days ago." If `IsRead` is a plain bool, the read *time* is lost. For MVP, we use `CreatedAt` as a proxy ("delete old read notifications"). A future iteration can add `ReadAt` for accuracy.
