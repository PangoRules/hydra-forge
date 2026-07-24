## Validate: ApiClientFactory + AuthDelegatingHandler + ErrorCollector

### Setup
- [ ] Build `src/HydraForge.Tui/HydraForge.Tui.csproj` successfully.
- [ ] Have HydraForge API running and reachable at the configured `ServerUrl`.
- [ ] A valid `TuiConfig` with `JwtToken` and `ExpiresAt` already persisted (use ConfigStore from Plan 2).

### Happy Path
1. Call `ApiClientFactory.CreateClient()` → returns a `HydraForgeApiClient` whose internal `HttpClient.BaseAddress` equals `config.ServerUrl.TrimEnd('/') + "/"`.
2. Inspect the returned client's first outgoing request (use Fiddler/mitmproxy or a `DelegatingHandler` test wrapper) → `Authorization: Bearer <jwt>` header is present.
3. Set `AuthDelegatingHandler.SetToken(null)` → subsequent requests omit the `Authorization` header.
4. Call `GetClient()` twice → second call returns the same cached `HydraForgeApiClient` instance.
5. Call `CreateUnauthenticatedClient()` → outgoing request has no `Authorization` header.
6. Trigger a successful login and call `RefreshTokenAsync()` → returns `true`, persisted `JwtToken` and `ExpiresAt` are updated, `_authHandler.SetToken(...)` is called with new token.
7. Issue an authenticated API call after refresh → request uses the new token (verified via header capture).
8. Call `IsTokenExpiringSoon()` when `ExpiresAt - UtcNow > 60s` → returns `false`.
9. Call `IsTokenExpiringSoon()` when `ExpiresAt - UtcNow < 60s` → returns `true`.
10. Add 51 errors to `ErrorCollector` via `Add(correlationId, message)` → `Count` is `50`, oldest entry is evicted.
11. Call `GetErrors()` → returns immutable list of `(Timestamp, CorrelationId, Message)` tuples in insertion order.
12. Call `Dismiss(0)` on a populated `ErrorCollector` → first entry is removed; `Count` decreases by 1.

### Edge Cases
1. Call `RefreshTokenAsync()` with empty `JwtToken` → returns `false` without throwing or hitting the API.
2. Call `RefreshTokenAsync()` against a server whose `/auth/refresh` returns 401 or 5xx → returns `false`, no exception leaks; existing token remains in config and handler.
3. Call `IsTokenExpiringSoon()` when `ExpiresAt` is `null` → returns `false`.
4. Call `Dismiss(index)` with `index < 0` or `index >= Count` → no-op, no exception.
5. Call `Dismiss(0)` on an empty collector → no-op, no exception.

### Regressions
1. Build TUI project → succeeds with zero warnings and zero errors.
2. NSwag codegen still runs and `HydraForgeApiClient.RefreshAsync` exists in `src/HydraForge.Tui/Generated/`.
3. `ConfigStore` round-trip still works (Plan 2 path unaffected).

### Cleanup
- [ ] Remove any test `TuiConfig` or persisted credentials written during validation.
