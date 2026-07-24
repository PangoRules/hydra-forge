## Validate: ConfigStore

### Setup
- [ ] Build `src/HydraForge.Tui/HydraForge.Tui.csproj` successfully.
- [ ] Run TUI process as current user on POSIX host.

### Happy Path
1. Ensure `~/.config/hydraforge/config.json` does not exist → `ConfigStore.Load()` returns default `TuiConfig`.
2. Call `ConfigStore.Save()` with populated server URL, JWT, expiry, and refresh token → directory and JSON file are created.
3. Inspect saved JSON → properties use camelCase and JSON is indented.
4. Inspect saved file mode on POSIX → mode is `0600`.
5. Call `ConfigStore.Load()` after save → populated values round-trip unchanged.
6. Call `ConfigStore.Clear()` → config file is deleted.

### Edge Cases
1. Call `ConfigStore.Clear()` when file is absent → no exception and file remains absent.
2. Replace config with invalid JSON, then call `Load()` → failure is surfaced without silently returning unrelated credentials.

### Regressions
1. Build TUI project → succeeds with zero warnings and zero errors.
2. Run TUI configuration/auth flow using `ConfigStore` → existing TUI startup remains functional.

### Cleanup
- [ ] Remove test config file and empty `~/.config/hydraforge` directory if created.
