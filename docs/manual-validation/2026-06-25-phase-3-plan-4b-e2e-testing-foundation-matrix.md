# Manual Validation Matrix: Plan 4B — E2E Testing Foundation

## Validate: Playwright e2e suite runs green locally and in CI

### Setup
- [ ] Postgres + MinIO up: `docker compose up -d postgres minio`
- [ ] API running with dev env: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HydraForge.Server` (must listen on `http://localhost:5116` per `launchSettings.json`)
- [ ] Web dev server up: `cd src/web-ui && NUXT_PUBLIC_API_BASE_URL=http://localhost:5116 pnpm dev`
- [ ] `cd src/web-ui && pnpm install` already run (so `playwright` is on PATH)
- [ ] One-time: `cd src/web-ui && pnpm exec playwright install --with-deps chromium`

### Happy Path
1. `pnpm exec playwright test --project=setup` → `e2e/.auth/testadmin.json` produced, contains non-empty `cookies` array with `auth_token`
2. `pnpm test:e2e` (all 5 specs) → all pass, no flakes, no manual waits left
3. Open `src/web-ui/playwright-report/` on a passing run → report renders, trace files present
4. Push branch and open PR → `e2e` job appears in Checks tab and passes (validates `ASPNETCORE_URLS: http://localhost:5000` + `wait-on http://localhost:5000` + `E2E_API_BASE_URL=http://localhost:5000` all align)

### Edge Cases
1. Local dev with API on default 5116 → `E2E_API_BASE_URL` unset, fixtures resolve to `http://localhost:5116`; all specs seed + run green
2. CI e2e job → API on 5000 (forced by `ASPNETCORE_URLS`), fixtures resolve to `http://localhost:5000`; `auth.setup.ts` posts to Nuxt's `/login`, not the API, so port is the web app's port (3000 via `pnpm preview --port 3000`)
3. Re-run `pnpm test:e2e` twice in a row without DB reset → second run also passes (random-suffix seeding avoids collisions; no `expect(count(N cards))` style assertions)
4. Concurrency spec run alone → opens two browser contexts, both auth'd, second save rejected with visible error in `[role="alert"]`

### Regressions
1. `pnpm typecheck && pnpm lint && pnpm test` (unit-test suite) → still green; Playwright specs live in `e2e/`, outside Vitest's `app/` glob
2. `pnpm build` → still green; no production code changes
3. Pre-existing e2e-blocking surface (`TestUserSeeder`, `auth_token` cookie, `ApiRoutes` constants) → untouched

### Cleanup
- [ ] No seeded projects/cards left behind in dev DB that need manual cleanup (suffixed titles make them identifiable; `Delete` button or direct DB truncate acceptable)
- [ ] `e2e/.auth/testadmin.json` is gitignored, no commit needed
- [ ] `playwright-report/` and `test-results/` are gitignored
