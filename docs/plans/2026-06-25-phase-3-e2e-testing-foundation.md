# E2E Testing Foundation (Playwright) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Branch:** `task/phase-3-e2e-foundation`
**Parent branch:** `feat/phase-3-web-ui`
**Parent spec:** `2026-06-23-phase-3-web-ui-design.md` — Task 4B

**Goal:** Stand up Playwright (open source, MIT license) as the project's first automated end-to-end layer, driving the real Nuxt dev server against the real .NET API + Postgres + MinIO stack, and convert the three highest-value sections of `docs/superpowers/manual-validation/phase-3-plan-3-matrix.md` (Save dirty-state/debounce, Archive/Restore, Concurrency/version conflict) into automated specs.

**Architecture:** Playwright drives a real browser against `pnpm dev` (Nuxt, `localhost:3000`) which in turn calls the real API (`localhost:5000`). There is no per-test database reset — every spec seeds its own project/column/card through the API directly (the same way `docs/superpowers/manual-validation/*.md` seeds via curl) and gives every seeded title a random suffix so repeated runs never collide. One spec (`auth.setup.ts`) logs in through the real UI once and saves the session as `storageState`; every other spec reuses that state instead of re-logging in. Seeded test users (`testadmin`/`TestAdmin123!`) only exist when the API runs with `ASPNETCORE_ENVIRONMENT=Development` (per `TestUserSeeder`, `CLAUDE.md`).

**Tech Stack:** `@playwright/test` (MIT)

## Global Constraints

- Open source only — `@playwright/test` is MIT-licensed; no paid dashboard/cloud features are used.
- Tests run against a **shared, non-reset** Postgres database. Every spec must create its own project via the API in a `beforeEach`/fixture and use `crypto.randomUUID()` (or `Date.now()`) suffixes in any title it asserts on. Never assert against "the board has N cards" — only against cards this spec itself created.
- `fullyParallel: false`, `workers: 1` for this first pass — there is no test-data isolation between spec files yet, so concurrent runs against the same DB would race. Revisit once/if a per-test database reset exists.
- The API must be running with `ASPNETCORE_ENVIRONMENT=Development` so `TestUserSeeder` has seeded `testadmin`/`TestAdmin123!`.
- API paths in fixtures/specs are built with plain template strings against the well-known `.NET` routes (`/api/Auth/login`, `/api/Projects`, etc.) — this is a separate Node/Playwright process, not the Nuxt app, so the `ApiRoutes` TypeScript constants from `app/lib/routes.ts` do not apply here; keep all such literal paths confined to `e2e/fixtures.ts` so there is exactly one place to update if a route changes.

---

## Task 1: Install and Configure Playwright

**Files:**
- Modify: `src/web-ui/package.json`
- Create: `src/web-ui/playwright.config.ts`
- Modify: `.gitignore`
- Create: `src/web-ui/e2e/smoke.spec.ts`

**Interfaces:**
- Produces: `pnpm test:e2e` script; `playwright.config.ts` with `baseURL` defaulting to `http://localhost:3000` (override via `E2E_BASE_URL`).

### Step 1: Install the package

```bash
cd src/web-ui && pnpm add -D @playwright/test
pnpm exec playwright install --with-deps chromium
```

### Step 2: Add the `test:e2e` script

In `src/web-ui/package.json`, add to `"scripts"`:

```json
"test:e2e": "playwright test"
```

### Step 3: Create `playwright.config.ts`

Create `src/web-ui/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'setup',
      testMatch: /.*\.setup\.ts/
    },
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'e2e/.auth/testadmin.json'
      },
      dependencies: ['setup']
    }
  ],
  webServer: process.env.CI
    ? undefined
    : {
        command: 'pnpm dev',
        url: 'http://localhost:3000',
        reuseExistingServer: true,
        timeout: 30000
      }
})
```

`webServer` is skipped in CI because the CI job (Task 6) starts `pnpm dev` itself before invoking Playwright, with explicit health-check waits on both the API and the web server.

### Step 4: Ignore Playwright artifacts and the saved session

Add to `.gitignore`:

```
src/web-ui/playwright-report/
src/web-ui/test-results/
src/web-ui/e2e/.auth/
```

### Step 5: Write a smoke spec to verify the setup

Create `src/web-ui/e2e/smoke.spec.ts`:

```ts
import { test, expect } from '@playwright/test'

test('login page renders', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'HydraForge' })).toBeVisible()
  await expect(page.getByLabel('Username')).toBeVisible()
  await expect(page.getByLabel('Password')).toBeVisible()
})
```

This spec has no `storageState` dependency — run it directly against the `setup` project's bare config to confirm Playwright itself is wired correctly before Task 2 adds the real auth fixture.

### Step 6: Run it

Pre-requisite (separate terminal or background): `docker compose up -d postgres minio && ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HydraForge.Server` and `cd src/web-ui && pnpm dev`.

Run: `cd src/web-ui && pnpm exec playwright test e2e/smoke.spec.ts --project=chromium --grep "login page renders"`
Expected: PASS (1 test). If it fails with a connection error, confirm both the API and `pnpm dev` are up on their default ports.

### Step 7: Commit

```bash
git add src/web-ui/package.json src/web-ui/pnpm-lock.yaml src/web-ui/playwright.config.ts \
  src/web-ui/e2e/smoke.spec.ts .gitignore
git commit -m "chore: install and configure Playwright for E2E testing"
```

---

## Task 2: Auth Setup Project and API Data-Seeding Fixture

**Files:**
- Create: `src/web-ui/e2e/auth.setup.ts`
- Create: `src/web-ui/e2e/fixtures.ts`

**Interfaces:**
- Produces: `e2e/.auth/testadmin.json` (storageState file, gitignored, regenerated every run by the `setup` project). `seedCard` fixture from `e2e/fixtures.ts` exporting `{ projectId: string, columnId: string, cardId: string, cardTitle: string }`, and a `test`/`expect` re-export so specs only need one import.

### Step 1: Write the auth setup spec

Create `src/web-ui/e2e/auth.setup.ts`:

```ts
import { test as setup, expect } from '@playwright/test'

const authFile = 'e2e/.auth/testadmin.json'

setup('authenticate as testadmin', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('Username').fill('testadmin')
  await page.getByLabel('Password').fill('TestAdmin123!')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/projects/)
  await page.context().storageState({ path: authFile })
})
```

### Step 2: Run the setup project to verify it produces a session file

Run: `cd src/web-ui && pnpm exec playwright test --project=setup`
Expected: PASS, and `src/web-ui/e2e/.auth/testadmin.json` exists afterward with a non-empty `cookies` array containing `auth_token`.

### Step 3: Write the data-seeding fixture

Create `src/web-ui/e2e/fixtures.ts`:

```ts
import { test as base, expect, type APIRequestContext } from '@playwright/test'

const API_BASE_URL = process.env.E2E_API_BASE_URL ?? 'http://localhost:5000'

interface SeededCard {
  projectId: string
  columnId: string
  cardId: string
  cardTitle: string
}

async function loginForApiToken(request: APIRequestContext): Promise<string> {
  const response = await request.post(`${API_BASE_URL}/api/Auth/login`, {
    data: { username: 'testadmin', password: 'TestAdmin123!' }
  })
  if (!response.ok()) {
    throw new Error(`E2E API login failed: ${response.status()} ${await response.text()}`)
  }
  const body = await response.json() as { accessToken: string }
  return body.accessToken
}

export const test = base.extend<{ seedCard: SeededCard }>({
  seedCard: async ({ playwright }, use) => {
    const request = await playwright.request.newContext()
    const token = await loginForApiToken(request)
    const headers = { Authorization: `Bearer ${token}` }
    const suffix = `${Date.now()}-${Math.floor(Math.random() * 10000)}`

    const projectResponse = await request.post(`${API_BASE_URL}/api/Projects`, {
      headers,
      data: { name: `E2E Project ${suffix}`, description: 'Created by Playwright' }
    })
    if (!projectResponse.ok()) {
      throw new Error(`E2E project seed failed: ${projectResponse.status()} ${await projectResponse.text()}`)
    }
    const project = await projectResponse.json() as { id: string }

    const columnsResponse = await request.get(`${API_BASE_URL}/api/projects/${project.id}/Columns`, { headers })
    const columns = await columnsResponse.json() as { id: string }[]
    const columnId = columns[0].id

    const cardTitle = `E2E Card ${suffix}`
    const cardResponse = await request.post(`${API_BASE_URL}/api/projects/${project.id}/Cards`, {
      headers,
      data: { columnId, title: cardTitle, description: '', type: 'Task' }
    })
    if (!cardResponse.ok()) {
      throw new Error(`E2E card seed failed: ${cardResponse.status()} ${await cardResponse.text()}`)
    }
    const card = await cardResponse.json() as { id: string }

    await use({ projectId: project.id, columnId, cardId: card.id, cardTitle })
    await request.dispose()
  }
})

export { expect }
```

### Step 4: Verify the fixture compiles and runs

Create a throwaway spec temporarily (do not commit it) to sanity-check the fixture end to end:

```ts
// e2e/_fixture-check.spec.ts (temporary, delete after this step)
import { test, expect } from './fixtures'

test('seeds a card via the API', async ({ seedCard }) => {
  expect(seedCard.cardId).toBeTruthy()
  expect(seedCard.cardTitle).toContain('E2E Card')
})
```

Run: `cd src/web-ui && pnpm exec playwright test e2e/_fixture-check.spec.ts --project=chromium`
Expected: PASS. Delete `e2e/_fixture-check.spec.ts` once green — it was only there to prove the fixture works before Tasks 3-5 build real specs on top of it.

### Step 5: Commit

```bash
git add src/web-ui/e2e/auth.setup.ts src/web-ui/e2e/fixtures.ts
git commit -m "feat: add Playwright auth setup and API data-seeding fixture"
```

---

## Task 3: Save Dirty-State & Debounce Spec (Matrix Section 3)

**Files:**
- Modify: `src/web-ui/app/components/card/CardModal.vue` (add `data-testid` hooks so specs can target the visible desktop pane without fighting Tailwind's `md:` breakpoint classes)
- Create: `src/web-ui/e2e/card-description-save.spec.ts`

**Interfaces:**
- Consumes: `seedCard` fixture from `e2e/fixtures.ts`.

**Why the `data-testid` change:** `CardModal.vue` renders **both** the desktop two-column layout and the mobile tabbed layout in the DOM at all times — only `hidden md:flex` / `md:hidden` CSS classes decide which one is visible at the current viewport. A Playwright locator like `page.getByRole('button', { name: 'Save' })` matches both copies and throws a strict-mode "resolved to 2 elements" error. A `data-testid` on each wrapper lets specs scope to the one that's actually visible at their chosen viewport.

### Step 1: Add `data-testid` to the two layout wrappers

In `src/web-ui/app/components/card/CardModal.vue`, add `data-testid="card-modal-desktop"` to the desktop wrapper div and `data-testid="card-modal-mobile"` to the mobile wrapper div:

```vue
<!-- Desktop: two-column with tabs in left pane -->
<div
  data-testid="card-modal-desktop"
  class="hidden md:flex max-h-[70vh] overflow-hidden"
>
```

```vue
<!-- Mobile: tabbed -->
<div
  data-testid="card-modal-mobile"
  class="md:hidden flex flex-col max-h-[70vh] overflow-hidden"
>
```

### Step 2: Write the spec

Create `src/web-ui/e2e/card-description-save.spec.ts`:

```ts
import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test.describe('Card description save button', () => {
  test('is disabled until the description is dirty, then saves and disables again', async ({ page, seedCard }) => {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

    const desktop = page.getByTestId('card-modal-desktop')
    const saveButton = desktop.getByRole('button', { name: 'Save' })

    await expect(saveButton).toBeDisabled()

    await desktop.locator('.ProseMirror').click()
    await page.keyboard.type('Updated from Playwright')

    await expect(saveButton).toBeEnabled()
    await saveButton.click()

    await expect(saveButton).toBeDisabled({ timeout: 10000 })
  })

  test('auto-saves after the debounce window without clicking Save', async ({ page, seedCard }) => {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

    const desktop = page.getByTestId('card-modal-desktop')
    const saveButton = desktop.getByRole('button', { name: 'Save' })

    await desktop.locator('.ProseMirror').click()
    await page.keyboard.type('Auto-saved text')

    await expect(saveButton).toBeEnabled()
    // Debounce is 2s (CardDescription.vue) — wait past it without clicking Save.
    await expect(saveButton).toBeDisabled({ timeout: 5000 })

    await page.reload()
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()
    await expect(page.getByTestId('card-modal-desktop')).toContainText('Auto-saved text')
  })
})
```

### Step 3: Run it

Run: `cd src/web-ui && pnpm exec playwright test e2e/card-description-save.spec.ts --project=chromium`
Expected: PASS (2 tests) against the real running stack from Task 1 Step 6.

### Step 4: Commit

```bash
git add src/web-ui/app/components/card/CardModal.vue src/web-ui/e2e/card-description-save.spec.ts
git commit -m "test(e2e): cover description save button dirty-state and debounced auto-save"
```

---

## Task 4: Archive / Restore Spec (Matrix Section 6)

**Files:**
- Create: `src/web-ui/e2e/card-archive-restore.spec.ts`

**Interfaces:**
- Consumes: `seedCard` fixture, `data-testid="card-modal-desktop"` from Task 3.

### Step 1: Write the spec

Create `src/web-ui/e2e/card-archive-restore.spec.ts`:

```ts
import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test('archiving a card removes it from the board, restoring brings it back', async ({ page, seedCard }) => {
  await page.goto(`/projects/${seedCard.projectId}/board`)
  await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

  await page.getByTitle('Archive card').click()
  await page.getByRole('button', { name: 'Archive' }).click() // ConfirmDialog confirm button

  await expect(page.getByRole('heading', { name: seedCard.cardTitle, exact: true })).not.toBeVisible()

  await page.reload()
  await page.getByRole('checkbox', { name: 'Archived only' }).first().check()
  await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()

  await expect(page.getByTitle('Restore card')).toBeVisible()
  await page.getByTitle('Restore card').click()

  await expect(page.getByText('Card restored')).toBeVisible()
})
```

Note: `ColumnHeader.vue`'s "Archived only" checkbox only renders when `includeArchived` is already true for that column (`v-if="includeArchived"`) — if this selector doesn't find the checkbox when this task is implemented, open the column's filter row first (the global `BoardFilterBar`'s "Show archived" toggle) before checking the per-column box; verify the exact control against the running board page and adjust the selector, the archive/restore assertions are the part of this spec that must not change.

### Step 2: Run it

Run: `cd src/web-ui && pnpm exec playwright test e2e/card-archive-restore.spec.ts --project=chromium`
Expected: PASS.

### Step 3: Commit

```bash
git add src/web-ui/e2e/card-archive-restore.spec.ts
git commit -m "test(e2e): cover archive and restore from the card modal"
```

---

## Task 5: Concurrency / Version-Conflict Spec (Matrix Section 7)

**Files:**
- Create: `src/web-ui/e2e/card-concurrency.spec.ts`

**Interfaces:**
- Consumes: `seedCard` fixture, `data-testid="card-modal-desktop"` from Task 3.

**Why this matters:** This is the regression test for the exact bug class `2026-06-25-phase-3-card-modal-hardening.md` (Task 2) fixes — two editors holding the same card, one saves, the other's save must be rejected with `409 CARD_CONCURRENCY_MISMATCH`, never silently overwrite the first save.

### Step 1: Write the spec

Create `src/web-ui/e2e/card-concurrency.spec.ts`:

```ts
import { test, expect } from './fixtures'

test.use({ viewport: { width: 1280, height: 800 } })

test('a stale save from a second tab is rejected, not silently merged', async ({ browser, seedCard }) => {
  const contextA = await browser.newContext({ storageState: 'e2e/.auth/testadmin.json' })
  const contextB = await browser.newContext({ storageState: 'e2e/.auth/testadmin.json' })
  const pageA = await contextA.newPage()
  const pageB = await contextB.newPage()

  for (const page of [pageA, pageB]) {
    await page.goto(`/projects/${seedCard.projectId}/board`)
    await page.getByRole('heading', { name: seedCard.cardTitle, exact: true }).click()
  }

  // Tab A saves first and succeeds.
  const descA = pageA.getByTestId('card-modal-desktop')
  await descA.locator('.ProseMirror').click()
  await pageA.keyboard.type('Saved from tab A')
  await descA.getByRole('button', { name: 'Save' }).click()
  await expect(descA.getByRole('button', { name: 'Save' })).toBeDisabled({ timeout: 10000 })

  // Tab B still holds the version it loaded with — its save must fail, not overwrite tab A's change.
  const descB = pageB.getByTestId('card-modal-desktop')
  await descB.locator('.ProseMirror').click()
  await pageB.keyboard.type('Saved from tab B (stale)')
  await descB.getByRole('button', { name: 'Save' }).click()

  await expect(descB.locator('[role="alert"]')).toBeVisible({ timeout: 10000 })

  await contextA.close()
  await contextB.close()
})
```

This spec depends on Task 4 of `2026-06-25-phase-3-card-modal-hardening.md` having landed (`role="alert"` on the save error) — if that plan has not yet been executed, replace `descB.locator('[role="alert"]')` with `descB.locator('.text-error')` to match the pre-hardening markup, then switch back once it lands.

### Step 2: Run it

Run: `cd src/web-ui && pnpm exec playwright test e2e/card-concurrency.spec.ts --project=chromium`
Expected: PASS.

### Step 3: Commit

```bash
git add src/web-ui/e2e/card-concurrency.spec.ts
git commit -m "test(e2e): cover stale-version save rejection across two sessions"
```

---

## Task 6: CI Integration

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: a new `e2e` job that runs after `dotnet` and `web-ui` succeed, on `pull_request` only (it is slower and heavier than the unit-test jobs, so it does not need to run on every push to a task branch).

### Step 1: Add the `e2e` job

In `.github/workflows/ci.yml`, add a new job after `web-ui`:

```yaml
  e2e:
    if: github.event_name == 'pull_request'
    runs-on: ubuntu-latest
    needs: [dotnet, web-ui]
    services:
      postgres:
        image: pgvector/pgvector:pg16
        env:
          POSTGRES_USER: hydraforge
          POSTGRES_PASSWORD: hydraforge
          POSTGRES_DB: hydraforge
        ports:
          - 5433:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - uses: pnpm/action-setup@v4
        with:
          version: 11.5.0
      - uses: actions/setup-node@v4
        with:
          node-version: "22"
          cache: pnpm
          cache-dependency-path: src/web-ui/pnpm-lock.yaml

      - name: Restore and build server
        run: |
          dotnet restore HydraForge.slnx
          dotnet build HydraForge.slnx --configuration Release --no-restore

      - name: Apply migrations
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5433;Database=hydraforge;Username=hydraforge;Password=hydraforge"
        run: |
          dotnet tool install --global dotnet-ef
          dotnet ef database update \
            --project src/HydraForge.Infrastructure \
            --startup-project src/HydraForge.Server

      - name: Start API server
        env:
          ASPNETCORE_ENVIRONMENT: Development
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5433;Database=hydraforge;Username=hydraforge;Password=hydraforge"
        run: |
          dotnet run --project src/HydraForge.Server --no-build --configuration Release &
          npx wait-on http://localhost:5000/openapi/v1.json --timeout 60000

      - name: Install web dependencies
        working-directory: src/web-ui
        run: pnpm install --frozen-lockfile

      - name: Install Playwright browsers
        working-directory: src/web-ui
        run: pnpm exec playwright install --with-deps chromium

      - name: Build and start web app
        working-directory: src/web-ui
        run: |
          pnpm build
          pnpm preview --port 3000 &
          npx wait-on http://localhost:3000/login --timeout 60000

      - name: Run Playwright tests
        working-directory: src/web-ui
        run: pnpm test:e2e

      - name: Upload Playwright report on failure
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: src/web-ui/playwright-report/
          retention-days: 7
```

This job intentionally skips MinIO — none of the three specs in this plan touch attachments. Add a `minio` service block the same way `postgres` is declared above if/when a future spec needs file uploads.

### Step 2: Verify

Push this branch and open a draft PR (or push to an existing PR) so the `pull_request` trigger fires; confirm the `e2e` job appears in the Checks tab and passes. If `dotnet ef database update` fails because `dotnet-ef` is already on the runner's `PATH` from a previous step's tool cache, drop the `dotnet tool install --global dotnet-ef` line — `actions/setup-dotnet` does not pre-install it, so the install step is normally required on a fresh runner.

### Step 3: Commit

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add Playwright e2e job on pull_request"
```

---

## Verification (Plan Complete)

1. `cd src/web-ui && pnpm test:e2e` — all 5 specs (smoke + description-save x2 + archive-restore + concurrency) pass locally against a running stack.
2. `cd src/web-ui && pnpm typecheck && pnpm lint && pnpm test` — unchanged unit-test suite still green (Playwright specs live in `e2e/`, outside Vitest's `app/` test glob).
3. Open a PR — the new `e2e` CI job runs and passes.
4. Re-run `pnpm test:e2e` a second time without restarting the database — all specs still pass (proves the random-suffix seeding avoids collisions).
