# Plan 10: Create / Edit / Move Cards via Keyboard

**Branch:** `task/tui-card-crud`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 10

**Goal:** Card create (n), edit title inline (e), move between columns (m), reorder (r), archive (Del) — all from board view keyboard.

**Depends on:** Task 7 (BoardScreen), Task 9 (CardDetailScreen for edit).

---

## Step 1: Add inline title edit to `BoardScreen`

Add method to `src/HydraForge.Tui/Screens/BoardScreen.cs`:

```csharp
private async Task EditCardTitleAsync()
{
    if (_columns.Count == 0) return;
    var col = _columns[_selectedColumn];
    if (_selectedCard >= col.Cards.Count) return;
    var card = col.Cards[_selectedCard];

    var newTitle = AnsiConsole.Prompt(
        new TextPrompt<string>("New title:")
            .DefaultValue(card.Title)
            .Validate(t => string.IsNullOrWhiteSpace(t)
                ? ValidationResult.Error("Title required")
                : ValidationResult.Success()));

    try
    {
        var client = _apiClientFactory.GetClient();
        var payload = new
        {
            title = newTitle,
            description = "",
            type = card.Type,
            parentCardId = (Guid?)null,
            dueAt = (DateTime?)null,
            version = card.Version
        };

        var response = await client.PutAsJsonAsync(
            $"api/projects/{_projectId}/cards/{card.Id}", payload, JsonOptions);

        if (response.IsSuccessStatusCode)
        {
            await LoadBoardAsync();
            await RenderAsync();
        }
    }
    catch (HttpRequestException ex)
    {
        _errorCollector.Add("N/A", $"Edit error: {ex.Message}");
    }
}
```

## Step 2: Add reorder mode to `BoardScreen`

Add field:
```csharp
private bool _reorderMode;
```

Add method:
```csharp
private async Task EnterReorderModeAsync()
{
    if (_columns.Count == 0) return;
    var col = _columns[_selectedColumn];
    if (_selectedCard >= col.Cards.Count) return;

    _reorderMode = true;
    AnsiConsole.MarkupLine("[yellow]Reorder mode: j/k to move, Enter to confirm, Esc to cancel[/]");
}

private async Task ConfirmReorderAsync()
{
    if (!_reorderMode) return;
    _reorderMode = false;

    var col = _columns[_selectedColumn];
    if (_selectedCard >= col.Cards.Count) return;
    var card = col.Cards[_selectedCard];

    try
    {
        var client = _apiClientFactory.GetClient();
        var payload = new
        {
            targetColumnId = col.Id,
            targetPosition = _selectedCard,
            confirmBlockedMove = false,
            version = card.Version
        };

        var response = await client.PostAsJsonAsync(
            $"api/projects/{_projectId}/cards/{card.Id}/move", payload, JsonOptions);

        if (response.IsSuccessStatusCode)
        {
            await LoadBoardAsync();
            await RenderAsync();
        }
    }
    catch (HttpRequestException ex)
    {
        _errorCollector.Add("N/A", $"Reorder error: {ex.Message}");
    }
}
```

## Step 3: Update `HandleKeyAsync` in `BoardScreen`

Add these cases to the switch in `HandleKeyAsync`:

```csharp
case ConsoleKey.E:
    if (!_reorderMode)
        await EditCardTitleAsync();
    break;

case ConsoleKey.R:
    if (!_reorderMode)
        await EnterReorderModeAsync();
    break;

case ConsoleKey.Enter:
    if (_reorderMode)
        await ConfirmReorderAsync();
    else
        await OpenCardDetailAsync();
    break;

case ConsoleKey.Escape:
    if (_reorderMode)
    {
        _reorderMode = false;
        await RenderAsync();
    }
    else
    {
        _appState.CurrentScreen = null;
    }
    break;
```

The existing `MoveCardAsync`, `CreateCardAsync`, `ArchiveCardAsync` methods are already in BoardScreen from Task 7. The `m`, `n`, `Del` keys are already wired.

## Step 4: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 5: Commit

```bash
git add src/HydraForge.Tui/Screens/BoardScreen.cs
git commit -m "feat(tui): add inline title edit, reorder mode, and card CRUD keyboard actions"
```