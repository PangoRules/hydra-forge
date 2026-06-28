# Manual Validation Matrix: Plan 4B — E2E Testing Foundation

## Validate: Playwright e2e suite runs green locally and in CI

### Setup — Local Dev Mode
- [ ] Postgres + MinIO up: `docker compose up -d postgres minio`
- [ ] `cd src/web-ui && pnpm install` already run (playwright on PATH)
- [ ] One-time: `cd src/web-ui && pnpm exec playwright install --with-deps chromium`
- [ ] Playwright's `webServer` auto-starts API + Nuxt; no need to run them separately

### Setup — Full Docker Stack
- [ ] `docker compose up` (starts postgres + minio + server + web)
- [ ] Access at `http://localhost:3000`
- [ ] API at `http://localhost:5000`
- [ ] For E2E: kill Docker web service or let Playwright start its own servers on host

### Happy Path
1. `pnpm exec playwright test --project=setup` → `e2e/.auth/testadmin.json` produced, contains non-empty `cookies` array with `auth_token`
2. `pnpm test:e2e` (all 5 specs) → all pass, no flakes, no manual waits left
3. Open `src/web-ui/playwright-report/` on a passing run → report renders, trace files present
4. Push branch and open PR → `e2e` job appears in Checks tab and passes

### Edge Cases
1. No API or Nuxt running beforehand → Playwright webServer starts both automatically, waits for health, runs tests, then cleans up
2. API or Nuxt already running (`reuseExistingServer: true`) → Playwright skips startup, reuses running instances
3. CI e2e job → separate e2e job in workflow (postgres service, migrations, Playwright); webServer is undefined in CI
4. Re-run `pnpm test:e2e` twice in a row without DB reset → second run also passes (random-suffix seeding avoids collisions)
5. Concurrency spec run alone → opens two browser contexts, both auth'd, second save rejected with visible error in `[role="alert"]`

### Regressions
1. `pnpm typecheck && pnpm lint && pnpm test` (unit-test suite) → still green; Playwright specs live in `e2e/`, outside Vitest's `app/` glob
2. `pnpm build` → still green; no production code changes
3. Pre-existing e2e-blocking surface (`TestUserSeeder`, `auth_token` cookie, `ApiRoutes` constants) → untouched
4. `docker compose up` builds clean, web service reaches API via Docker network (`http://server:8080`)
5. `docker compose up -d postgres minio` (local dev) still works — no breaking changes to existing services

### Cleanup
- [ ] No seeded projects/cards left behind in dev DB that need manual cleanup (suffixed titles make them identifiable)
- [ ] `e2e/.auth/testadmin.json` is gitignored, no commit needed
- [ ] `playwright-report/` and `test-results/` are gitignored
