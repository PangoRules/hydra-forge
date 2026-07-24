# Plan 1: TUI Scaffold + Spectre.Console + NSwag Codegen

**Branch:** `task/tui-scaffold`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 1

**Goal:** Strip bare console template, add NuGet packages, set up NSwag codegen (MSBuild target + nswag.json + dotnet tool manifest), bootstrap `Program.cs` with Spectre.Console app loop.

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

## Step 3: Create dotnet tool manifest for NSwag

Create `.config/dotnet-tools.json` at repo root (if it doesn't exist) or add the nswag tool entry:

```bash
dotnet new tool-manifest --output .config 2>/dev/null || true
dotnet tool install NSwag.Console --version 14.2.0 --tool-path .config/dotnet-tools.json 2>/dev/null || true
```

If `.config/dotnet-tools.json` already exists, add the `nswag.console` entry manually:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "nswag.console": {
      "version": "14.2.0",
      "commands": ["nswag"]
    }
  }
}
```

Run: `dotnet tool restore` to verify the tool is available.

## Step 4: Create NSwag config file

Create `src/HydraForge.Tui/nswag.json`:

```json
{
  "runtime": "Net90",
  "documentGenerator": {
    "fromDocument": {
      "url": "http://localhost:5000/openapi/v1.json",
      "output": null
    }
  },
  "codeGenerators": {
    "openApiToCSharpClient": {
      "clientBaseClass": null,
      "configurationClass": null,
      "generateClientClasses": true,
      "generateClientInterfaces": true,
      "clientBaseInterface": null,
      "injectHttpClient": true,
      "disposeHttpClient": false,
      "generateExceptionClasses": true,
      "exceptionClass": "ApiException",
      "wrapDtoExceptions": true,
      "useHttpClientCreationMethod": false,
      "httpClientType": "System.Net.Http.HttpClient",
      "useHttpRequestMessageCreationMethod": false,
      "useBaseUrl": false,
      "generateBaseUrlProperty": false,
      "generateSyncMethods": false,
      "generatePrepareRequestAndProcessResponseAsAsyncMethods": false,
      "exposeJsonSerializerSettings": false,
      "clientClassAccessModifier": "public",
      "typeAccessModifier": "public",
      "generateContractsOutput": true,
      "contractsNamespace": "HydraForge.Tui.Generated",
      "contractsOutputFilePath": "Generated/Contracts.cs",
      "className": "HydraForgeApiClient",
      "operationGenerationMode": "MultipleClientsFromOperationId",
      "additionalNamespaceUsages": [],
      "additionalContractNamespaceUsages": [],
      "generateOptionalParameters": true,
      "generateJsonMethods": false,
      "encodeContractNames": false,
      "generateDataAnnotations": false,
      "excludedTypeNames": [],
      "excludedParameterNames": [],
      "handleReferences": false,
      "generateImmutableArrayProperties": false,
      "generateImmutableDictionaryProperties": false,
      "jsonSerializerSettingsTransformationMethod": null,
      "inlineNamedDictionaries": false,
      "inlineNamedAny": false,
      "generateDtoTypes": true,
      "generateOptionalPropertiesAsNullable": true,
      "generateNullableReferenceTypes": true,
      "templateDirectory": null,
      "typeNameGeneratorType": null,
      "propertyNameGeneratorType": null,
      "enumNameGeneratorType": null,
      "serviceHost": null,
      "serviceSchemes": null,
      "output": "Generated/HydraForgeApiClient.cs",
      "newLineBehavior": "Auto",
      "namespace": "HydraForge.Tui.Generated"
    }
  }
}
```

## Step 5: Add GenerateApiClient MSBuild target

Add to `src/HydraForge.Tui/HydraForge.Tui.csproj` (after `</ItemGroup>`):

```xml
  <Target Name="GenerateApiClient" BeforeTargets="BeforeBuild" Condition="'$(DesignTimeBuild)' != 'true'">
    <Exec
      Command="dotnet nswag run nswag.json"
      WorkingDirectory="$(MSBuildProjectDirectory)"
      ContinueOnError="true"
      EchoOff="false" />
    <Message Text="NSwag codegen complete. Check Generated/ for output." Importance="high" />
  </Target>
```

- `ContinueOnError="true"` — if server not running, build continues with last generated client (spec line 425)
- `Condition="'$(DesignTimeBuild)' != 'true'"` — skip during IDE design-time builds
- `WorkingDirectory="$(MSBuildProjectDirectory)"` — runs from TUI project dir where `nswag.json` lives

## Step 6: Create directory structure

```bash
mkdir -p src/HydraForge.Tui/{Screens,Services,Models,Renderers,Generated}
```

## Step 7: Create `IScreen` interface

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

## Step 8: Create `AppState` model

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

## Step 9: Create `ScreenStack` model

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

## Step 10: Create `TuiConfig` model

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

## Step 11: Bootstrap `Program.cs`

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

## Step 12: Build verification

```bash
dotnet tool restore
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds. NSwag codegen may warn if server not running (non-fatal). Generated files appear in `src/HydraForge.Tui/Generated/`.

## Step 13: Commit

```bash
git add src/HydraForge.Tui/ .config/dotnet-tools.json
git commit -m "feat(tui): scaffold project with Spectre.Console, NSwag codegen, models, and screen interface"
```