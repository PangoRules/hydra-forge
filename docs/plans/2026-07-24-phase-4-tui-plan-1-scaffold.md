# Plan 1: TUI Scaffold + Spectre.Console Setup

**Branch:** `task/tui-scaffold`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 1

**Goal:** Strip bare console template, add NuGet packages, set up NSwag codegen, bootstrap `Program.cs` with Spectre.Console app loop.

**Depends on:** Nothing.

---

## Step 1: Remove Application project reference

Modify `src/HydraForge.Tui/HydraForge.Tui.csproj` — remove the `ProjectReference` to `HydraForge.Application`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

## Step 2: Add NuGet packages

Add to `src/HydraForge.Tui/HydraForge.Tui.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spectre.Console" Version="0.49.*" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.*" />
    <PackageReference Include="NSwag.ApiDescription.Client" Version="14.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

Run: `dotnet restore src/HydraForge.Tui/HydraForge.Tui.csproj`

## Step 3: Create directory structure

```bash
mkdir -p src/HydraForge.Tui/{Screens,Services,Models,Renderers,Generated}
```

## Step 4: Create `IScreen` interface

Create `src/HydraForge.Tui/Screens/IScreen.cs`:

```csharp
namespace HydraForge.Tui.Screens;

public interface IScreen
{
    Task RenderAsync();
    Task HandleKeyAsync(ConsoleKeyInfo key);
    Task OnEnterAsync();
    Task OnExitAsync();
}
```

## Step 5: Create `AppState` model

Create `src/HydraForge.Tui/Models/AppState.cs`:

```csharp
namespace HydraForge.Tui.Models;

public enum ConnectionStatus { Connected, Reconnecting, Disconnected }

public class AppState
{
    public IScreen? CurrentScreen { get; set; }
    public Guid? SelectedProjectId { get; set; }
    public Guid? SelectedCardId { get; set; }
    public ConnectionStatus Connection { get; set; } = ConnectionStatus.Disconnected;
    public List<(DateTime Timestamp, string CorrelationId, string Message)> Errors { get; } = new();
    public int OnlineCount { get; set; }
    public int UnreadNotifications { get; set; }
}
```

## Step 6: Create `ScreenStack` model

Create `src/HydraForge.Tui/Models/ScreenStack.cs`:

```csharp
namespace HydraForge.Tui.Models;

public class ScreenStack
{
    private readonly Stack<IScreen> _stack = new();

    public void Push(IScreen screen) => _stack.Push(screen);
    public IScreen? Pop() => _stack.Count > 0 ? _stack.Pop() : null;
    public IScreen? Peek() => _stack.Count > 0 ? _stack.Peek() : null;
    public int Count => _stack.Count;
    public void Clear() => _stack.Clear();
}
```

## Step 7: Create `TuiConfig` model

Create `src/HydraForge.Tui/Models/TuiConfig.cs`:

```csharp
namespace HydraForge.Tui.Models;

public class TuiConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public string? JwtToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
}
```

## Step 8: Bootstrap `Program.cs`

Replace `src/HydraForge.Tui/Program.cs`:

```csharp
using HydraForge.Tui.Models;
using Spectre.Console;

namespace HydraForge.Tui;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var appState = new AppState();
        var screenStack = new ScreenStack();

        AnsiConsole.Write(new FigletText("HydraForge").Color(Color.Blue));
        AnsiConsole.WriteLine("TUI client starting...");

        // Placeholder: will wire up screens in later tasks
        AnsiConsole.WriteLine("Press any key to exit (placeholder).");
        Console.ReadKey(true);

        return 0;
    }
}
```

## Step 9: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds with no errors.

## Step 10: Commit

```bash
git add src/HydraForge.Tui/
git commit -m "feat(tui): scaffold project with Spectre.Console, models, and screen interface"
```