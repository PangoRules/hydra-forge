# Plan 14: Comments — Inline View + Add

**Branch:** `task/tui-comments`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 14

**Goal:** Inline threaded comment view in card detail, add comment via prompt.

**Depends on:** Task 3 (ApiClientFactory), Task 9 (CardDetailScreen already has comment display and add).

---

## Step 1: Verify existing comment functionality in `CardDetailScreen`

The `CardDetailScreen` from Task 9 already includes:
- `BuildCommentsPanel()` — renders comments with author, timestamp, content
- `AddCommentAsync()` — prompts for comment text, POSTs to API
- `LoadCommentsAsync()` — fetches comments from API
- `a` key wired to `AddCommentAsync()` when comments section is active

No additional code needed. This task is already implemented by Task 9.

## Step 2: Add comment count to board card display (optional enhancement)

Update `BoardRenderer.CardData` to include comment count:

In `src/HydraForge.Tui/Renderers/BoardRenderer.cs`, add field:
```csharp
public record CardData(
    Guid Id,
    int CardNumber,
    string Title,
    string Type,
    bool IsBlocked,
    List<string> AssigneeInitials,
    int Version,
    int CommentCount = 0
);
```

Update card markup in `BuildLayout`:
```csharp
var commentBadge = card.CommentCount > 0
    ? $" [grey]💬{card.CommentCount}[/]"
    : "";

var cardMarkup = $"{prefix}{typeBadge} #{card.CardNumber} {Markup.Escape(title)}{assignees}{commentBadge}";
```

In `BoardScreen.LoadBoardAsync`, load comment counts (optional — can skip for performance):
```csharp
// Skip per-card comment count fetch for performance.
// Comment count shown only in card detail view.
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Renderers/BoardRenderer.cs
git commit -m "feat(tui): add comment count badge to board cards"
```

Note: If no changes needed (comment functionality fully covered by Task 9), skip commit and mark task complete.