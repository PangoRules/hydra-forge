## Validate: JWT Role Claim Fix

### Setup
- [ ] API server running (`dotnet run --project src/HydraForge.Server`) with seeded admin user
- [ ] `dotnet build` exits 0
- [ ] `dotnet test --filter "FullyQualifiedName~JwtTokenIssuerTests"` passes 3/3

### Happy Path
1. POST `/api/auth/login` with seeded admin credentials → 200 with `accessToken`
2. Decode the returned JWT payload (jwt.io) → `role` claim present with value `"Admin"`, `is_admin` claim still present with value `"true"`
3. Connect to `/hubs/board` with the admin's JWT and call `JoinProject(<any projectId>)` → succeeds without HubException (admin bypasses membership check)

### Edge Cases
1. POST `/api/auth/login` with a non-admin seeded user → 200; decoded JWT has **no** `role` claim, `is_admin` equals `"false"`
2. Non-admin connects to `/hubs/board` and calls `JoinProject(<projectIdWithoutMembership>)` → server throws `HubException("Access denied")`, call rejects client-side
3. Use an existing pre-fix admin-issued JWT (if any cached) → `IsInRole("Admin")` returns false (expected: must re-login to mint token with new claim)

### Regressions
1. Non-admin user with valid project membership calls `JoinProject` on `/hubs/board` and `/hubs/presence` → still works (membership path unaffected)
2. Existing `/api/auth/login` response shape, expiry, and `is_admin` flag unchanged for both admin and non-admin users
3. `dotnet test` (full suite) → all prior tests still pass, no unintended DI or startup failures from the new `AddAuthorization` policy

### Cleanup
- [ ] No persistent state added; nothing to remove
