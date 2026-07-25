## Validate: ProjectListScreen (list, create, search, archived filter, sort, role filter, pagination)

### Setup
- [ ] Build `src/HydraForge.Tui/HydraForge.Tui.csproj` successfully (zero warnings, zero errors).
- [ ] Valid `TuiConfig` with a working JWT (login already completed via Plan 4 flow) and `ServerUrl` pointing at a running API with at least a few existing projects.
- [ ] `HydraForgeApiClient.ProjectsGETAsync` / `ProjectsPOSTAsync` generated (Plan 3 / NSwag codegen present) and typed against the corrected enum schema (D-50) — `MemberRole`, `ColumnTemplate` are real C# enums, not `int`.

### Happy Path
1. Launch TUI with a valid session → `ProjectListScreen` renders immediately after auth, showing a table of the user's projects (`#`, Name, Members, Role, Created) and a footer with `N projects total`.
2. Press `j`/`↓` repeatedly → selection highlight (`[blue bold]`) moves down one row at a time, stops at the last row.
3. Press `k`/`↑` repeatedly → selection moves up, stops at the first row.
4. Press `g` → jumps to first row. Press `Shift+G` → jumps to last row. `Home`/`End` behave the same as `g`/`Shift+G`.
5. Press `c` → prompted for name (required), description (optional), template (`General`/`Software`/`Blank`) → on submit, `POST /api/projects` fires, success message shown, list reloads and includes the new project.
6. Press `/` → prompted for a search string → list reloads filtered server-side (`search` query param) and the header shows `Filter: "..."`.
7. Press `a` → toggles `includeArchived` and reloads; archived projects show a `(archived)` badge and an `A` marker in the last column. Press `a` again → archived projects drop back out.
8. Press `Enter` on a row → `AppState.SelectedProjectId` is set and a placeholder "Opening project: ..." message prints (board screen is Task 7 — this is expected, not a bug).
9. Press `q` → confirm prompt → `Environment.Exit(0)` on "Yes"; "No" leaves the screen running.

### Pagination, Sort, Role Filter (added this session)
10. With more than 20 projects (page size matches the Web UI's `docs/specs/2026-07-07-project-list-redesign-design.md` convention): footer reads `Showing 1-20 of N projects (page 1/M)`. Press `n` → advances to page 2, `Showing 21-N of N projects (page 2/M)`. Press `n` again at the last page → no-op (no extra API call). Press `p` → goes back a page; no-op at page 1.
11. Header shows `Sort: CreatedAt ↓   Role: All` by default. Press `s` repeatedly → cycles `CreatedAt → UpdatedAt → Name → CreatedAt`, each triggering a reload with the new `sortBy`. Press `Shift+S` → toggles `↓`/`↑` (`sortDescending`), reloads.
12. Press `r` repeatedly → cycles `Role: All → Owner → Member → All`, each reloading with the corresponding `role` filter.
13. Changing any filter (`a`, `/`, `s`, `Shift+S`, `r`) resets to page 1 and resets row selection to the top — confirmed no stale `skip` carries over from a previous page.
14. Reopening search (`/`) pre-fills the current filter value instead of starting blank.

### Error Visibility (D-50 follow-up — see this session)
15. Force a load failure (e.g. corrupt the saved JWT) → red "Errors (N)" panel renders below the footer with message, timestamp, and correlationId; footer gains a `[x] Dismiss errors` hint.
16. Press `x` with errors present → all errors clear, panel disappears, hint drops from the footer.
17. Trigger a create-project failure (e.g. stop the server mid-prompt) → error recorded and rendered the same way, not swallowed.

### Regressions
1. `LoginScreen` (Plan 4) and `LockScreen`/`ConnectionManager` (Plan 5) still behave as documented in their own matrices — this screen only replaces the post-auth placeholder in `Program.cs`.
2. `ApiClientFactory.GetClient()` still returns a cached, authenticated client.
3. `IScreen` contract unchanged (`OnEnterAsync`, `OnExitAsync`, `RenderAsync`, `HandleKeyAsync`).
4. `HydraForge.Tui.csproj` has no `ProjectReference` to `HydraForge.Domain`/`HydraForge.Application` — the TUI stays a pure HTTP client (fixed this session; was briefly reintroduced then removed).

### Cleanup
- [ ] Delete any test projects created while validating (e.g. via the API directly, or archive+ignore if hard delete isn't wired up yet).
