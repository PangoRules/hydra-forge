## Validate: TUI Scaffold (Plan 1 — Spectre.Console + NSwag + Models)

### Setup
- [ ] `dotnet tool restore` at repo root succeeds (installs `nswag.consolecore` 14.7.1)
- [ ] `dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj` succeeds with 0 errors
- [ ] `src/HydraForge.Tui/Generated/HydraForgeApiClient.cs` and `Contracts.cs` exist after build
- [ ] API server NOT required for build (codegen fails silently, build still succeeds)
- [ ] Docker Postgres+MinIO not required for this plan

### Happy Path
1. `dotnet run --project src/HydraForge.Tui` → Figlet "HydraForge" in blue renders, "TUI client starting..." line appears, any key exits cleanly with exit code 0
2. `dotnet test tests/HydraForge.Tui.Tests` → 6 ScreenStackTests pass
3. `dotnet test` (full solution) → 412 tests pass (188 Application + 6 TUI + 71 Infrastructure + 147 Server)
4. `grep -n "ProjectReference" src/HydraForge.Tui/HydraForge.Tui.csproj` → no Application reference
5. `grep -rn "Newtonsoft.Json" src/HydraForge.Tui/Generated/Contracts.cs` → properties annotated with `[Newtonsoft.Json.JsonProperty(...)]`
6. `grep -n 'int? Position { get; set; } = "0";' src/HydraForge.Tui/Generated/Contracts.cs` → no match (sed fix applied)
7. `grep -n "Stack<IScreen>" src/HydraForge.Tui/Models/ScreenStack.cs` → matches line 7

### Edge Cases
1. Build with API server not running on :5000 → NSwag codegen warns/exits non-zero, build still succeeds (ContinueOnError=true), Generated/ files reflect last-good state
2. Build twice in a row on a clean checkout → second build succeeds, Generated files are idempotent
3. `dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` → "No changes have been made to the model since the last migration"
4. Inspect `src/HydraForge.Tui/Generated/Contracts.cs` for any `int? Position { get; set; } = "0";` defaults that escaped the sed patch → none

### Regressions
1. `dotnet build` of full solution → still builds (HydraForge.Tui fails to load if csproj ref breaks)
2. `dotnet test` of non-TUI projects → still 188 + 71 + 147 = 406 pass
3. `HydraForge.slnx` opens in IDE/editor → Tui project and Tests project both visible
4. No `using HydraForge.Application`/`Domain`/`Infrastructure`/`Server` in any non-Generated TUI source file (boundary check)
5. `git log --oneline feat/phase-4-tui..task/tui-scaffold` → only the 2 expected commits

### Cleanup
- [ ] None — no DB, no file uploads, no test data created
- [ ] Branch `task/tui-scaffold` ready for merge into `feat/phase-4-tui`
