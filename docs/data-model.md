# HydraForge — Data Model

> **Version:** 1.0
> **Date:** 2026-06-03
> **Database:** PostgreSQL 16 + pgvector extension

---

## Table of Contents

1. [Entity Relationship Overview](#1-entity-relationship-overview)
2. [Project Space Entities](#2-project-space-entities)
3. [Chat & Collaboration Entities](#3-chat--collaboration-entities)
4. [LLM & Routing Entities](#4-llm--routing-entities)
5. [Personal Space Entities](#5-personal-space-entities)
6. [Enums](#6-enums)

---

## 1. Entity Relationship Overview

```
User (1) ──┬── (N) ProjectMember
           ├── (N) ChatSession
           ├── (N) ChatFolder
           ├── (N) AgentPersonality
           ├── (N) MemoryEntry
           ├── (N) Note
           ├── (N) PersonalTask
           ├── (N) CalendarSource ──── (N) CalendarEvent
           ├── (N) Document ──── (N) DocumentVersion
           │                └── (N) DocumentChunk        ← RAG embeddings
           ├── (N) GalleryImage
           ├── (N) Album ──── (N) GalleryImage (via AlbumImage)
           ├── (1) UserTokenBudget
           ├── (N) TokenUsageRecord
           └── (N) ImageUsageRecord

Project (1) ──┬── (N) Column
              ├── (N) Card
              ├── (N) Spec ──── (N) SpecVersion
              │       └── (N) Plan (ownership via Plan.SpecId)
              ├── (N) Plan ──── (N) PlanVersion
              ├── (N) AuditLogEntry
              ├── (N) ProjectMember
              ├── (1) ChatFolder          ← auto-created on project creation
              └── (1) ProjectContextSnapshot

Column (1) ── (N) Card

Card (1) ──┬── (N) Comment
           ├── (N) ChecklistItem
           ├── (N) CardAssignee
           ├── (N) Attachment
           ├── (N) ChildCard (self-referencing via ParentCardId)
           ├── (N) CardChatLink
           ├── (N) CardWatcher
           ├── (N) CardRelationship (as source)
           ├── (N) CardRelationship (as target)
           ├── (N) Spec (ownership via Spec.CardId)
           └── (N) Plan (ownership via Plan.CardId)

ChatFolder (1) ──┬── (N) ChatFolder (self-referencing, max depth 2)
                 └── (N) ChatSession

ChatSession (1) ──┬── (N) ChatMessage
                  ├── (N) CardChatLink
                  └── (1) Project? (when in project folder)

Note (1) ──┬── (N) NoteReminder
           └── (N) NoteImageAttachment

AuditLogEntry (N) ── (1) Project
Notification (N) ── (1) User
LlmProvider — global, admin-managed provider connection
ProviderModelConfig — global, admin-managed model catalog row for one provider/model pairing
FeatureRoutingConfig — routing policy row per AiFeature, derived from default tier assignment requirements
```

---

## 2. Project Space Entities

### Project

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| Name | string | Project name |
| Description | string | Short description |
| GitRemoteUrl | string? | Optional git remote URL |
| GitProvider | string? | e.g. "github", "gitlab", "gitea", "self-hosted" |
| Columns | Column[] | Nav property — ordered list of columns (FK: Column.ProjectId) |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### Column

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| ProjectId | Guid | FK to Project |
| Name | string | e.g. "Backlog", "In Dev", "Done" |
| Position | int | Ordering (0-based) |
| WipLimit | int? | Optional WIP limit (future) |
| Color | string? | Hex color for visual distinction |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### Card

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| CardNumber | int | Sequential per project (e.g. #1, #42) — unique within project, never reused |
| ProjectId | Guid | FK to Project |
| ColumnId | Guid | FK to Column |
| ParentCardId | Guid? | FK to parent card (epic → child) |
| Title | string | |
| Description | string | Markdown content |
| Type | CardType | Task / Issue / Goal / Idea |
| Position | int | Order within column |
| DueAt | DateTime? | Optional due date/time. Aligned with `PersonalTask.DueAt`, `CalendarEvent.StartAt`, `NoteReminder.TriggerAt`. |
| Version | int | Optimistic concurrency / increment per edit |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| MovedAt | DateTime | When it last changed column |
| ArchivedAt | DateTime? | Soft-delete marker; archived cards remain visible to project members, hard-deleted by housekeeping job after admin-configured retention period |

### CardAssignee

| Field | Type | Description |
|---|---|---|
| CardId | Guid | FK to Card |
| UserId | Guid | FK to User |
| AssignedAt | DateTime | |
| AssignedByUserId | Guid | Who made the assignment |

### CardRelationship

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SourceCardId | Guid | FK to Card — the card that has the dependency |
| TargetCardId | Guid | FK to Card — the card being depended on |
| Type | RelationshipType | `BlockedBy`, `Precedes`, `Relates` |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who established the relationship (human or AI agent) |
| ArchivedAt | DateTime? | Set when source or target card is archived — soft delete, retained for audit |

> Both cards must belong to the same project. Application layer validates acyclic graph on every insert.

> Future: extend to full DAG with critical path calculation without schema changes — the directed graph is already represented.

### Comment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| AuthorId | Guid | |
| Content | string | Markdown |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker set by the author when they want to hide the comment; archived comments remain visible (with an "archived" badge) and can be restored by the author, hard-deleted by housekeeping job after admin-configured retention period |

### ChecklistItem

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| Text | string | |
| IsCompleted | bool | |
| Position | int | |
| AssignedTo | Guid? | FK to User (optional per-item assignee) |
| CreatedAt | DateTime | |

### Attachment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| FileName | string | Original filename |
| Size | long | Bytes |
| ContentType | string | MIME type |
| StoragePath | string | Local FS path or S3 key |
| UploadedByUserId | Guid | FK to User |
| CreatedAt | DateTime | |

### Spec

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| CardId | Guid | FK to Card — owning card (the card that created this spec) |
| Title | string | Display name, e.g. "Auth Module Spec" |
| Description | string? | Optional description |
| Content | string | Current markdown content |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### SpecVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SpecId | Guid | FK to Spec |
| Title | string | Title at time of snapshot |
| Description | string? | Description at time of snapshot |
| Content | string | Full markdown snapshot at this version |
| Version | int | Matches Spec.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who saved this version |

### Plan

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project |
| CardId | Guid | FK to Card — owning card (the card that created this plan) |
| SpecId | Guid? | FK to Spec — optional parent specification |
| Title | string | Display name, e.g. "Auth Implementation Plan" |
| Content | string | Current markdown (numbered steps) |
| Version | int | Increments on each edit |
| CreatedByUserId | Guid | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

### PlanVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| PlanId | Guid | FK to Plan |
| Title | string | Title at time of snapshot |
| Description | string? | Description at time of snapshot |
| Content | string | Full markdown snapshot at this version |
| Version | int | Matches Plan.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | Who saved this version |

### AuditLogEntry

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Scope | AuditLogScope | `Project`, `System`, or `Personal` |
| ProjectId | Guid? | Required when Scope = `Project`; null for `System` and `Personal` |
| ActorId | Guid | Who performed the action |
| Action | string | Human-readable, e.g. "Moved card 'Fix login' from Backlog to In Dev" |
| EntityType | string | "Card", "Column", "Spec", "Plan" |
| EntityId | Guid | |
| OldValue | string? | JSON snapshot before |
| NewValue | string? | JSON snapshot after |
| Timestamp | DateTime | When the event occurred (semantic) |
| CreatedAt | DateTime | When the row was persisted (typically == `Timestamp`; both kept for clarity) |

### ProjectMember

| Field | Type | Description |
|---|---|---|
| ProjectId | Guid | FK to Project |
| UserId | Guid | FK to User |
| Role | MemberRole | `Owner` / `Member` |
| JoinedAt | DateTime | |

### ProjectContextSnapshot

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ProjectId | Guid | FK to Project (unique) |
| TemplateContent | string | Template-rendered board state: columns, card index (id+number+title+column+type), open blockers, recent moves. Regenerated on every board mutation — no LLM call, instant. |
| AiNarrative | string? | AI-generated end-of-day summary: "5 cards moved, 2 blockers resolved, PR created for #42." Generated nightly by scheduled job. Nullable — null until first nightly run. |
| TemplateGeneratedAt | DateTime | Set on every board mutation |
| AiNarrativeGeneratedAt | DateTime? | Set by nightly job |

> **Rendering:** `ProjectContextSnapshotRenderer` (Application layer, `static`, pure deterministic) — reads columns, cards, and active `CardRelationship`s, outputs JSON. No LLM, no DB writes, no side effects. **Refresh pipeline:** `IProjectSnapshotRefresher.RefreshAsync()` is called by all 9 mutation services (Project, Column, Card, Checklist, Comment, Attachment, Spec, Plan, CardRelationship) immediately after persisting changes. `IProjectSnapshotRefresher.GetSnapshotAsync()` serves the `GET /api/projects/{projectId}/ProjectSnapshot` endpoint (members-only).

### CardWatcher

| Field | Type | Description |
|---|---|---|
| CardId | Guid | FK to Card |
| UserId | Guid | FK to User |
| AddedAt | DateTime | Auto-added on comment or assignment |

### Notification

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Recipient |
| Title | string | Short title (e.g. "Card moved", "Comment added") |
| Body | string? | Optional longer detail line shown in the bell-dropdown |
| Message | string | Human-readable text — historical field kept for parity with older notifications; new code prefers `Title` + `Body` |
| CardId | Guid? | |
| ProjectId | Guid? | |
| ActionUrl | string? | Optional deep-link target (e.g. `/projects/.../cards/42`); clients use this for the notification click handler |
| IsRead | bool | |
| CreatedAt | DateTime | |

---

## 3. Chat & Collaboration Entities

### User

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| Username | string | Unique, case-insensitive |
| FirstName | string? | Display first name |
| LastName | string? | Display last name |
| PasswordHash | string | bcrypt / Argon2 |
| IsAdmin | bool | Admin role |
| IsDisabled | bool | Admin can disable access without deleting |
| CreatedAt | DateTime | |
| LastLoginAt | DateTime? | |

### ChatFolder

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Name | string | Display name |
| OwnerId | Guid | FK to User |
| ParentFolderId | Guid? | FK to self — max depth 2 enforced at app layer |
| ProjectId | Guid? | Set when auto-created by project; null for free-form folders |
| CreatedAt | DateTime | |
| ArchivedAt | DateTime? | Set on project archive |

### ChatSession

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Title | string | |
| OwnerId | Guid | FK to User |
| FolderId | Guid? | FK to ChatFolder |
| ProjectId | Guid? | FK to Project when session is in a project folder |
| IsShared | bool | True for project-folder chats (visible to all members read-only) |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | |

### ChatMessage

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| SessionId | Guid | FK to ChatSession |
| Role | MessageRole | `User` / `Assistant` / `System` |
| Content | string | Message text (markdown for assistant, plain for user) |
| InputTokens | int | Tokens in this request (0 for user messages) |
| OutputTokens | int | Tokens in this response (0 for user messages) |
| CachedTokens | int | Prompt cache hits for this call |
| ModelName | string? | Model used (null for user messages) |
| CreatedAt | DateTime | |

### CardChatLink

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| CardId | Guid | FK to Card |
| ChatSessionId | Guid | FK to ChatSession |
| OwnerId | Guid | FK to User (chat owner) |
| Summary | string | Auto-generated summary of what was discussed |
| CreatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived links remain visible (so archived cards still show "discussed in chat X"), hard-deleted by housekeeping job after admin-configured retention period |

### AgentPersonality

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | e.g. "Senior Dev Mode", "Concise" |
| Description | string? | User-facing summary of when to use this personality |
| SystemPrompt | string | Injected at chat start after project context |
| IsDefault | bool | One per user |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; null while active |

---

## 4. LLM & Routing Entities

### LlmProvider

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| Name | string | Display name, e.g. "Company OpenAI" |
| BaseUrl | string | e.g. `https://api.openai.com/v1` |
| ApiKeyEncrypted | string | Encrypted at rest |
| IsEnabled | bool | Admin toggle |
| AdapterType | AdapterType | `OpenAiCompatible` / `Anthropic` / `Ollama` / `Diffusers` / `ComfyUi` |
| ProviderType | ProviderType | `Text` / `Image` / `Both` |
| Tier | ModelTier | `Economy` / `Standard` / `Premium` |
| FallbackProviderId | Guid? | Used if this provider is rate-limited or unavailable |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

> Provider model lists are not persisted on `LlmProvider`. Provider probes can be fetched live for admin selection; `ProviderModelConfig` is the atomic configured model catalog used by routing, pricing, enablement, and context-window limits.

### ProviderModelConfig

| Field | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| ProviderId | Guid | FK to `LlmProvider`; identifies which provider offers this model |
| ModelId | string | Provider/API-facing model identifier, e.g. `gpt-4.1`, `claude-sonnet-4`, `llama3.3` |
| Name | string | Human-friendly display name shown to admins and users |
| Tier | ModelTier | Admin-assigned tier: `Economy`, `Standard`, or `Premium` |
| PricePerToken | decimal? | Optional pricing metadata for cost estimation and usage reporting |
| MaxTokens | int? | Optional model token limit used by context-window guard and routing decisions |
| IsEnabled | bool | Admin toggle; disabled models remain configured but are not available for routing |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

> `ProviderModelConfig` is the configured model catalog. One provider can expose many models, and each model can have its own tier, limit, pricing, and enablement state.

### FeatureRoutingConfig

| Field | Type | Description |
|---|---|---|
| Feature | AiFeature | e.g. `PersonalChat`, `ProjectChat`, `DeepResearch`, `AgentPipeline`, `MemoryExtraction` |
| DefaultTier | ModelTier | Install default tier for the feature |
| MaxUserTier | ModelTier? | Ceiling for user overrides — null means locked to default |

> `FeatureRoutingConfig` remains planned for the model-routing work that implements FR-172 and FR-173.

### UserTokenBudget

| Field | Type | Description |
|---|---|---|
| UserId | Guid | FK to User |
| DailyLimit | int? | Token cap per day (null = unlimited) |
| MonthlyLimit | int? | Token cap per month (null = unlimited) |

### TokenUsageRecord

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Who triggered the call |
| Feature | AiFeature | Which feature made the call |
| ProviderModelConfigId | Guid | Configured provider/model row selected by routing |
| ProviderId | Guid | Which LLM provider was used |
| ModelId | string | Provider/API-facing model identifier copied from `ProviderModelConfig.ModelId` |
| ModelName | string | Historical display name copied from `ProviderModelConfig.Name` |
| InputTokens | int | |
| OutputTokens | int | |
| CachedTokens | int | Prompt cache hits (reduces effective cost) |
| Cost | decimal | Recorded estimated/provider-reported call cost |
| ProjectId | Guid? | Set if call was in project context |
| PipelineRunId | Guid? | Correlation id grouping multiple LLM calls in one agent pipeline run |
| CreatedAt | DateTime | |

> `ProviderId`, `ModelId`, and `ModelName` are intentionally denormalized. They preserve the exact historical provider/model used even if a `ProviderModelConfig` row is renamed, disabled, repriced, or removed later.

> `PipelineRunId` is a nullable correlation id, not a foreign key. It groups multi-turn agent pipeline calls (Planner → Developer → Reviewer) so admin reporting can show one aggregate pipeline cost.

### ImageUsageRecord

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | Who triggered the generation |
| Feature | AiFeature | `ImageChat`, `ImageGalleryEditor`, or `ImageDocument` |
| ProviderModelConfigId | Guid | Configured provider/model row selected by routing |
| ProviderId | Guid | Which image provider was used |
| ModelId | string | Provider/API-facing model identifier copied from `ProviderModelConfig.ModelId` |
| ModelName | string | Historical display name copied from `ProviderModelConfig.Name` |
| ImageCount | int | Number of images generated |
| Resolution | string | e.g. `1024x1024`, `1920x1080` |
| Cost | decimal | Recorded estimated/provider-reported generation cost |
| ProjectId | Guid? | Set if triggered in project context |
| CreatedAt | DateTime | |

> One row per image generation request, not per generated image. Used for admin usage dashboards, per-feature cost attribution, and provider billing reconciliation.

---

## 5. Personal Space Entities

### MemoryEntry

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Category | MemoryCategory | `Fact`, `Preference`, `Identity`, `Event`, `Contact`, `Instruction` |
| Content | string | The memory text |
| Embedding | vector(1536) | pgvector — for semantic search |
| IsPinned | bool | Pinned memories injected first in context |
| Source | string? | e.g. "auto-extracted", "manual", session ID |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived memories remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period |

### Note

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Content | string | Markdown (checklist items inline as `- [ ]`) |
| IsPinned | bool | |
| SortOrder | int | Drag-reorder position |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived notes remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period. Replaces the old `IsArchived: bool` field; the timestamp is needed for retention policy. |

### NoteReminder

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| NoteId | Guid | FK to Note |
| TriggerAt | DateTime | Next trigger time |
| RepeatPattern | string? | Cron expression or `daily`/`weekly`/`monthly` (null = one-time) |
| LastTriggeredAt | DateTime? | |
| IsSent | bool | `true` once the trigger has fired (or once a recurring reminder is disabled by `NoteArchiveService`); housekeeping hard-deletes one-shot reminders 30 days after this flips true |
| CreatedAt | DateTime | |

### NoteImageAttachment

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| NoteId | Guid | FK to Note |
| FilePath | string | Storage path |
| CreatedAt | DateTime | |

### PersonalTask

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Title | string | |
| Description | string? | |
| IsCompleted | bool | |
| DueAt | DateTime? | |
| CronExpression | string? | Recurring schedule (null = one-time) |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived tasks remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period |

### CalendarSource

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | Display name |
| Color | string | Hex color |
| CalDavUrl | string? | CalDAV endpoint (null = local-only calendar) |
| CalDavUsername | string? | |
| CalDavPasswordEncrypted | string? | |
| ExternalUrl | string? | Optional public URL to view the source externally |
| WebhookSecret | string? | Secret for validating incoming webhook callbacks (e.g. provider push notifications); null if the source does not support webhooks |
| LastSyncAt | DateTime? | Last successful CalDAV pull/sync timestamp (data freshness) |
| CreatedAt | DateTime | Row creation time |
| UpdatedAt | DateTime | Last metadata change (rename, color, credentials, etc.); distinct from `LastSyncAt` |
| ArchivedAt | DateTime? | Soft-delete marker; archived sources stop syncing but their events remain visible, hard-deleted by housekeeping job after admin-configured retention period |

### CalendarEvent

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| CalendarSourceId | Guid | FK to CalendarSource |
| ExternalUid | string? | CalDAV UID for sync (null = local event) |
| Title | string | |
| Description | string? | |
| StartAt | DateTime | |
| EndAt | DateTime | |
| IsAllDay | bool | |
| RecurrenceRule | string? | iCal RRULE string |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived events remain visible to the user (e.g. cancelled), hard-deleted by housekeeping job after admin-configured retention period |

### Document

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Title | string | |
| ContentType | string | `pdf` / `markdown` / `code` / `csv` / `html` |
| FilePath | string? | Storage path for uploaded binary files (PDF, etc.) |
| Content | string? | Text content for editable documents |
| Language | string? | Programming language for code documents |
| Version | int | Increments on each edit |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived documents remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period. Replaces the old `IsArchived: bool` field; the timestamp is needed for retention policy. |

### DocumentVersion

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| DocumentId | Guid | FK to Document |
| Content | string | Full content snapshot |
| Version | int | Matches Document.Version at time of snapshot |
| CreatedAt | DateTime | |
| CreatedByUserId | Guid | |

### DocumentChunk

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User (for security scoping) |
| DocumentId | Guid | FK to `Document` when `SourceType = "document"`; denormalized for indexed lookup. Polymorphic `SourceType` + `SourceId` is the authoritative pairing. |
| SourceType | string | `document` / `note` / `memory` |
| SourceId | Guid | FK to source entity (resolves to `Document`, `Note`, or `MemoryEntry` per `SourceType`) |
| ChunkIndex | int | Order within source document |
| Content | string | ~500-token text chunk |
| Embedding | vector(1536) | pgvector — for RAG similarity search |
| CreatedAt | DateTime | |

> `DocumentChunk` powers RAG: at chat time, user message is embedded → similarity search on user's chunks → top-K results injected as context. Chunks regenerated when source document changes.

### GalleryImage

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| FilePath | string | Storage path |
| OriginalFilename | string | |
| ContentType | string? | IANA MIME type, e.g. `image/jpeg`, `image/png`, `image/webp`, `image/heic` — kept as string to allow any format the server can detect (no enum; matches `Attachment.ContentType`) |
| ThumbnailPath | string? | Storage path to generated thumbnail; null until first thumbnail render |
| Hash | string | SHA-256 of file bytes — for deduplication |
| Size | long | Bytes |
| Width | int | |
| Height | int | |
| TakenAt | DateTime? | From EXIF |
| CameraModel | string? | From EXIF |
| Latitude | double? | From EXIF GPS |
| Longitude | double? | From EXIF GPS |
| IsFavorite | bool | |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived images remain visible to the user (including in archived albums), hard-deleted by housekeeping job after admin-configured retention period |

### Album

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| UserId | Guid | FK to User |
| Name | string | |
| Description | string? | Optional summary of album contents |
| CoverImageId | Guid? | FK to GalleryImage |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived albums remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period |

### AlbumImage

| Field | Type | Description |
|---|---|---|
| AlbumId | Guid | FK to Album |
| ImageId | Guid | FK to GalleryImage |
| Position | int | Order within album |
| CreatedAt | DateTime | |
| ArchivedAt | DateTime? | Soft-delete marker; archived links remain visible to the user, hard-deleted by housekeeping job after admin-configured retention period |

### ImageTag

| Field | Type | Description |
|---|---|---|
| Id | Guid | |
| ImageId | Guid | FK to GalleryImage |
| Tag | string | |
| Source | TagSource | `User` / `AI` |
| CreatedAt | DateTime | |

### SystemSettings

Singleton row (Id = `00000000-0000-0000-0000-000000000001`) holding admin-configurable retention knobs consumed by the housekeeping background service. Future system-wide settings can be added as columns without schema change elsewhere.

| Field | Type | Description |
|---|---|---|
| Id | Guid | Singleton id (fixed) |
| ArchivedItemRetentionDays | int | Days an item stays in the `ArchivedAt` state before housekeeping hard-deletes it. Default `730` (2 years). |
| AuditLogRetentionDays | int | Days audit log and LLM/image usage records are kept. Default `90` (satisfies NFR-7). |
| NotificationRetentionDays | int | Days a read notification is kept before housekeeping purges it. Default `30`. |
| CreatedAt | DateTime | |
| UpdatedAt | DateTime | |

---

## 6. Enums

### CardType
| Value | Name  | Description                               |
|-------|-------|-------------------------------------------|
| 1     | Task  | Unit of work                              |
| 2     | Issue | Problem, concern, or question             |
| 4     | Idea  | Suggestion; may become a Goal or Task     |
| 5     | Goal  | Significant objective; groups child cards |

> Value 3 (Spec) was retired in migration `MigrateSpecCardsToGoal`; existing rows moved to Goal (5).

### RelationshipType
`BlockedBy`, `Precedes`, `Relates`

### MessageRole
`User`, `Assistant`, `System`

### MemberRole
`Owner`, `Member`

### MemoryCategory
`Fact`, `Preference`, `Identity`, `Event`, `Contact`, `Instruction`

### TagSource
`User`, `AI`

### AdapterType
`OpenAiCompatible`, `Anthropic`, `Ollama`, `Diffusers`, `ComfyUi`

### ProviderType
`Text`, `Image`, `Both`

### ModelTier
`Economy`, `Standard`, `Premium`

### AiFeature

Shared domain enum used to connect routing policy, token/image usage records, and admin usage dashboards without relying on free-form feature strings.

Values: `PersonalChat`, `ProjectChat`, `DeepResearch`, `AgentPipeline`, `MemoryExtraction`, `NotesClassification`, `DocumentEditing`, `CardReview`, `ImageChat`, `ImageDocument`, `ImageGalleryEditor`
