# Plan 12: Blocked Card Indicator in Board View

**Branch:** `task/tui-blocked-indicator`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 12

**Goal:** Show 🔴 prefix on cards that have active "BlockedBy" relationships in board view.

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
    // Fetch relationships for all cards to find BlockedBy
    foreach (var card in cards)
    {
        var relResponse = await client.GetAsync(
            $"api/projects/{_projectId}/cards/{card.Id}/relationships");
        if (relResponse.IsSuccessStatusCode)
        {
            var relList = await relResponse.Content
                .ReadFromJsonAsync<RelationshipList>(JsonOptions);
            if (relList?.Relationships.Any(r => r.Type == "BlockedBy") == true)
            {
                blockedCardIds.Add(card.Id);
            }
        }
    }
}
catch { /* Non-critical — blocked indicators are cosmetic */ }
```

Update the `CardData` construction to use `blockedCardIds`:
```csharp
.Select(c => new BoardRenderer.CardData(
    c.Id,
    c.CardNumber,
    c.Title,
    c.Type,
    blockedCardIds.Contains(c.Id), // IsBlocked
    c.Assignees.Select(a => a.Username[..1].ToUpper()).ToList(),
    c.Version
))
```

## Step 2: Add DTO for relationship list

Add to `BoardScreen`:
```csharp
private record RelationshipList(List<RelationshipInfo> Relationships);
private record RelationshipInfo(string Type);
```

## Step 3: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 4: Commit

```bash
git add src/HydraForge.Tui/Screens/BoardScreen.cs
git commit -m "feat(tui): add blocked card indicator (🔴) in board view"
```