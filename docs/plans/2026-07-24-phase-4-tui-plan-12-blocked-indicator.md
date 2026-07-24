# Plan 12: Blocked Card Indicator in Board View

**Branch:** `task/tui-blocked-indicator`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 12

**Goal:** Show 🔴 prefix on cards that have active "BlockedBy" relationships in board view. Uses NSwag-generated `HydraForgeApiClient`.

**Depends on:** Task 7 (BoardScreen), Task 11 (dependency panel creates relationships).

---

## Step 1: Load relationship data in `BoardScreen.LoadBoardAsync`

Update `LoadBoardAsync` in `src/HydraForge.Tui/Screens/BoardScreen.cs` — after loading cards, load relationships to determine blocked status:

Add after the cards load:
```csharp
// Load blocked card IDs
var blockedCardIds = new HashSet<Guid>();
try
{
    var client = _apiClientFactory.GetClient();

    // Fetch relationships for all cards to find BlockedBy
    foreach (var card in cards)
    {
        var relList = await client.RelationshipsAllAsync(_projectId, card.Id);
        if (relList?.Relationships != null)
        {
            foreach (var rel in relList.Relationships)
            {
                if (rel.Type == "BlockedBy")
                    blockedCardIds.Add(card.Id);
            }
        }
    }
}
catch
{
    // Non-fatal — blocked indicators are cosmetic
}
```

Then update the `CardData` construction to use `blockedCardIds`:
```csharp
.Select(c => new BoardRenderer.CardData(
    c.Id,
    c.CardNumber,
    c.Title,
    c.Type,
    blockedCardIds.Contains(c.Id), // ← was hardcoded false
    c.Assignees?.Select(a => a.Username[..1].ToUpper()).ToList() ?? new(),
    c.Version
))
```

**Key change:** `client.RelationshipsAllAsync(projectId, cardId)` — typed NSwag method instead of raw `client.GetAsync(...)`.

## Step 2: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 3: Commit

```bash
git add src/HydraForge.Tui/Screens/BoardScreen.cs
git commit -m "feat(tui): add blocked card indicator via relationship check in board view"
```