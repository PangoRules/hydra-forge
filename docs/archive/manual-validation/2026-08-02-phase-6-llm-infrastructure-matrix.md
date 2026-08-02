# Consolidated Manual Validation Matrix — Phase 6: LLM Infrastructure

Consolidates the per-plan matrices written during Phase 6 (24 tasks) into one document, per the `docs/manual-validation/` → `docs/archive/manual-validation/` convention (see `CLAUDE.md` § Manual validation matrices). Source files: `2026-07-30-phase-6-llm-infrastructure-matrix.md`, `-design-matrix.md`, `-plan-2-key-vault-matrix.md`, `-plan-19-webui-admin-routing-matrix.md` (all deleted from `docs/manual-validation/` in this pass) plus new rows for Task 22's AiNarrative UI surfaces (Web UI modal + TUI viewer, D-58) and the `[ProducesResponseType]` fix that unblocked them.

**Focus: Web UI E2E.** Phase 6 has almost no TUI surface (one new screen, one keybinding) — the bulk of user-facing work is the five admin pages, the account usage page, and the board-header narrative modal. Adapter/service-level correctness (SSE parsing, cache-block hashing, workflow JSON shapes, routing algorithms) is exhaustively covered by 900+ automated unit/integration tests (`dotnet test`) and is summarized rather than re-transcribed step-by-step here — this document's job is what automated tests *can't* cover: real browser clicks, real Hangfire dashboard, real cross-service wiring.

## 0. Setup (once, for the whole matrix)

- [ ] `docker compose up -d postgres minio` (or full stack `docker compose up`)
- [ ] `Llm__EncryptionKey` set — either in `.env` (Docker) or shell env / `appsettings.Development.json` (`dotnet run`): a base64-encoded 32-byte AES-256 key (`openssl rand -base64 32`)
- [ ] `dotnet build` clean, `dotnet csharpier check .` clean
- [ ] `dotnet test` — all projects green (Domain, Application, Infrastructure, Server, Tui)
- [ ] Server running: `dotnet run --project src/HydraForge.Server` (or `docker compose up -d --build server` if validating the Docker image — **remember the `server`/`web` Compose services need `--build` to pick up new code, they don't hot-reload**)
- [ ] Web UI running: `cd src/web-ui && pnpm dev`
- [ ] Logged in as `testadmin` / `TestAdmin123!` (admin role) in one session, `testuser1` / `TestUser123!` (non-admin) in another
- [ ] At least one `LlmProvider` row configured (any `AdapterType`) with a `ProviderModelConfig` so routing/usage pages have data to render

---

## 1. Web UI E2E

### 1.1 Admin — LLM Providers (`/admin/providers`)

| # | Steps | Expected |
|---|---|---|
| 1 | Navigate to `/admin/providers` as admin | Table loads, columns include `adapterType`, `providerType`, `tier` |
| 2 | Click "Add Provider" → fill name, base URL, adapter type, provider type, tier, API key → save | New provider appears in table |
| 3 | Click an existing provider → edit name/URL → save | Changes persist on reload |
| 4 | Edit a provider — API key field | Field is empty/write-only; previously-set key is never echoed back |
| 5 | Toggle "Disable" on a provider | Row shows disabled state, `IsEnabled = false` (verify via `GET /api/admin/llm/providers`) |
| 6 | Click "Probe Models" on an OpenAI-compatible provider with a real key | Live model list returned from provider's `/models` (or empty list if unconfigured — not an error) |
| 7 | Non-admin navigates to `/admin/providers` | Redirected away (no admin access) |

### 1.2 Admin — Provider Models (`/admin/provider-models`)

| # | Steps | Expected |
|---|---|---|
| 1 | Navigate to a provider's model list | Table shows configured `ProviderModelConfig` rows |
| 2 | Add model — fill Model ID, display name, tier, price/token, max tokens → save | Model appears in list |
| 3 | Edit an existing model's tier/price/max-tokens → save | Changes persist on reload |
| 4 | Delete a model with confirmation dialog | Model removed from list; routing no longer offers it |
| 5 | Toggle model enabled/disabled | Disabled model excluded from `ModelRouter` resolution (verify via a routed call, or by inspecting `IsEnabled` in the row) |

### 1.3 Admin — AI Feature Routing (`/admin/routing`)

| # | Steps | Expected |
|---|---|---|
| 1 | Navigate to `/admin/routing` | 11 rows, one per `AiFeature`, human-readable names from the feature label map |
| 2 | Change "Personal Chat" Default Tier Economy → Standard | Toast "Routing updated", select reflects new value, persists on reload |
| 3 | Change "Project Chat" Max User Tier Premium → Locked | Toast success, select shows "Locked (default only)", reload shows `null` |
| 4 | Change Max User Tier Locked → Premium on a different row | Persists correctly |
| 5 | Edit Default Tier on row A, then immediately edit Default Tier on row B before row A's save settles | Both rows show success toast and persist correctly — regression guard for a shared-counter bug where editing a second row silently dropped the first row's toast/state update |
| 6 | Network failure during save (throttle/offline in devtools) | Toast error, select reverts to previous value |
| 7 | Non-admin navigates to `/admin/routing` | Redirected to `/projects` |

### 1.4 Admin — Usage Dashboard (`/admin/usage`)

| # | Steps | Expected |
|---|---|---|
| 1 | Navigate to `/admin/usage` | "Token Usage" tab active by default; table loads with timestamp/user/feature/model/input/output/cached tokens/cost columns |
| 2 | Footer | "Total Cost: $X.XXXX" aggregate shown |
| 3 | Switch to "Image Usage" tab | Table switches to image columns (timestamp, user, feature, model, image count, resolution, cost); total cost refreshes |
| 4 | Type partial username into user filter (debounced ~300ms) | Dropdown shows matches → click one → filter chip with clear (×) button appears, table refreshes |
| 5 | Clear user filter via chip × | Filter clears, dropdown reopens |
| 6 | Open feature multi-select, pick 2 features | URL/query params include both features (check Network tab), table refreshes |
| 7 | Set From/To date filters | Table refreshes to the date-bounded result set |
| 8 | Click "Reset filters" | All filters clear, page resets to 1 |
| 9 | Change page size, click next page | `skip` increments correctly, filters preserved across pagination |
| 10 | Backend down (stop server) mid-session | Error toast fires, table stays in last known state (no crash) |
| 11 | Non-admin navigates to `/admin/usage` | Redirected away |

### 1.5 Account Self-Service Usage (`/account/usage`)

| # | Steps | Expected |
|---|---|---|
| 1 | Log in as non-admin, open user menu → "Usage" | Navigates to `/account/usage`, header "My Usage" |
| 2 | Page content | Period dates render; token + image usage bars visible; Recent Calls table populates (≤ 20 rows, timestamp/feature/model/tokens/images/cost) |
| 3 | Formatting | Token/image cells comma-formatted; cost column `$X.XXXX` |
| 4 | User with `tokensBudget == 0` | Bar shows "Unlimited" instead of a fill percentage |
| 5 | User with zero usage history | Bars show 0%/Unlimited, Recent Calls renders empty state (no crash) |
| 6 | Two different logged-in users | Each only ever sees their own usage rows — no cross-user bleed |

### 1.6 AI Project Narrative — Web UI (D-58, Task 22)

| # | Steps | Expected |
|---|---|---|
| 1 | Open any project's board view | Sparkle icon button ("View AI narrative") visible next to the project title, next to the archived badge slot |
| 2 | Click the button before any narrative has been generated | Modal opens, shows "No AI narrative has been generated for this project yet. Narratives are generated nightly." — no error, no crash |
| 3 | Trigger `ai-narrative-gen` from `/hangfire` (see §3 below), wait for completion, reopen the modal | Modal shows the generated narrative text and "Generated {timestamp}" |
| 4 | Close the modal (× or footer "Close") | Modal closes; board keyboard shortcuts re-enable (modal no longer blocks `useBoardKeyboardNav`) |
| 5 | Regression check: `GET /api/projects/{id}/ProjectSnapshot` via curl or Network tab | Response has a populated OpenAPI schema (`ProjectSnapshotResponse` with `aiNarrative`/`aiNarrativeGeneratedAt` fields) — this endpoint was missing `[ProducesResponseType]` until Task 22 review, which meant NSwag/openapi-typescript generated no typed response for either client to bind to; this row guards against that regressing |

---

## 2. TUI E2E

Phase 6 has one new TUI surface: the AI narrative viewer (D-58). Everything else (adapters, routing, admin) is server/Web-UI-only — the TUI never talks to LLM providers directly (CLAUDE.md: "Server is the only component that calls LLMs").

| # | Steps | Expected |
|---|---|---|
| 1 | Open TUI, navigate to a project's board, press `?` | Help overlay lists `v — View AI narrative` alongside existing bindings |
| 2 | Press `v` on a project with no narrative yet | Overlay shows "No AI narrative has been generated for this project yet. Narratives are generated nightly.", `[Esc] Back` / `[q] Quit` hints |
| 3 | Press `v` on a project with a generated narrative | Overlay shows narrative text in a bordered panel, header shows "Generated {timestamp}" |
| 4 | Narrative longer than one screen | `j`/`k` (or ↑/↓) scroll the text; scroll clamps at start/end (no out-of-range panel crash) |
| 5 | Press `Esc` from the narrative overlay | Returns to the board screen, cursor position preserved |
| 6 | Terminal resized while narrative overlay is open, then re-rendered | `ConsoleSize.Sync()` picks up the new size — no `ArgumentOutOfRangeException` (same guard class as `BoardRenderer`, though this screen doesn't use `Layout` so the failure mode doesn't apply — verify anyway) |

---

## 3. AiNarrative Job — End to End (Hangfire → DB → both clients)

| # | Steps | Expected |
|---|---|---|
| 1 | Visit `/hangfire` as admin | Dashboard loads; `"ai-narrative-gen"` listed under Recurring Jobs with the configured `Cron.Daily(h, m)` schedule |
| 2 | Visit `/hangfire` as non-admin (or logged out) | Rejected — `AdminRequiredAuthFilter` blocks; logged-out user redirected to login (no `auth_token` cookie) |
| 3 | Trigger `"ai-narrative-gen"` manually from the dashboard | Job runs, completes with no red/failed entries within ~30s for a small number of projects |
| 4 | Query DB after the run: `SELECT "AiNarrative", "AiNarrativeGeneratedAt" FROM project_context_snapshots WHERE "ProjectId" = <id>` | `AiNarrative` populated (3–5 sentence narrative), `AiNarrativeGeneratedAt` within the last few minutes UTC |
| 5 | Query `token_usage_records` for that run | A row exists with `Feature = ProjectChat`, `ProjectId` = the project, `UserId = 00000000-0000-0000-0000-000000000000` (system/batch identity — not charged against any real user's `UserTokenBudget`) |
| 6 | Open the same project in Web UI / TUI (§1.6 / §2) | Narrative visible in both clients immediately (no cache lag — both fetch fresh on modal/overlay open) |
| 7 | Trigger with a project that has no `ProjectContextSnapshot` row yet | Job skips it silently — no exception, no partial write |
| 8 | Trigger with an archived project in the mix | Archived project skipped; `AiNarrative` unchanged for it |
| 9 | Trigger with no LLM providers configured (or `ModelRouter.ResolveAsync` failing) | Job logs a warning per project and completes — no crash, no dashboard failure |
| 10 | One project's LLM call throws mid-batch (simulate: disable its provider mid-run) | That project's failure is logged; remaining projects in the batch still process normally (per-project isolation, not one bad project aborting the whole run) |
| 11 | Restart the server after changing `SystemSettings.AiNarrativeGenerationTimeUtc` via `/admin/settings` | New schedule takes effect on the registration-at-startup pass (known limitation: not live — see `CLAUDE.md` Hangfire patterns) |

---

## 4. Encryption & Startup Guards

| # | Steps | Expected |
|---|---|---|
| 1 | Start server with `Llm:EncryptionKey` missing | Server refuses to start — `InvalidOperationException` with a clear message, not a generic crash |
| 2 | Start with key set to `"not-base64!!!"` | Same refusal |
| 3 | Start with a valid-base64 but wrong-length key (16 or 24 bytes) | Same refusal |
| 4 | Start with a correct 32-byte base64 key | Server starts cleanly |
| 5 | `IKeyVault.Encrypt`/`Decrypt` round-trip via any provider's API key field (add/edit in Admin Providers UI) | Key saved encrypted (`v1:{nonce}:{ciphertext}:{tag}` format), decrypts correctly at call time — never logged or echoed in plaintext |

---

## 5. Backend / Adapter Coverage (automated — spot-check only)

All items below are fully covered by `dotnet test` (900+ tests across 6 adapters, `ModelRouter`, `ContextCompressor`, `EfUsageRecorder`, `LlmAdminService`, `LlmClientFactory`). Manual spot-checks here are for the **live, non-mocked paths** that unit tests fake out — real provider calls, if credentials are available. Skip any row without a live API key; it's not a gap, the code path is still unit-tested via HTTP fakes.

| Area | Live-only manual check (optional, needs real credentials) |
|---|---|
| `OpenAiCompatibleAdapter` | Real OpenAI/Groq/etc. key → `StreamChatAsync` yields real deltas; second identical call reuses the `[cache:{hex}]` prefix |
| `AnthropicAdapter` | Real `ANTHROPIC_API_KEY` → `claude-*` model streams real text; second call shows non-zero `CachedTokens` (cache hit) |
| `OllamaAdapter` | Local Ollama with a pulled model → real streaming + `GetModelsAsync` returns the local tag list |
| `DallEAdapter` | Real `OPENAI_API_KEY` → `GenerateImageAsync` returns a real `https://*.openai.com/...` image URL |
| `StabilityAiAdapter` | Real Stability key → `GenerateImageAsync`/`InpaintAsync` return real base64 image data |
| `ComfyUiAdapter` | Live ComfyUI server → full `/prompt` submit → `/history` poll → `/view` fetch round-trip for both text-to-image and inpaint workflows |
| `LlmClientFactory.EmbeddingFor` | Real OpenAI-compatible key → `/v1/embeddings` returns vectors with correct dimensionality |
| `ModelRouter` fallback chain | Seed a real 2-provider fallback chain, disable the primary provider, confirm a real request routes to the fallback |

---

## 6. Cross-Cutting Regression

- [ ] `dotnet ef migrations has-pending-model-changes` → clean (no drift between entities and latest migration)
- [ ] All existing `/api/admin/*` endpoints (users, projects, audit-log, settings) still work
- [ ] Existing admin pages not touched by Phase 6 (`/admin/users`, `/admin/audit-log`) still load
- [ ] Board view, project list, SignalR realtime, checklist/comments/attachments — unaffected by Phase 6 changes
- [ ] Hangfire dashboard does **not** mount in `Test` environment (`WebApplicationFactory` fixtures boot without a real Postgres connection string and must not try `AddHangfire`)
- [ ] All 14+ `WebApplicationFactory` test fixtures still set `Llm:EncryptionKey` in `ConfigureWebHost` and boot cleanly
- [ ] `docker compose up -d --build server web` picks up all Phase 6 code (server + web images rebuilt, not stale)

## Cleanup

- [ ] Unset any manually-exported `Llm__EncryptionKey` shell var after testing
- [ ] Remove any test `LlmProvider` / `ProviderModelConfig` rows created for manual validation
- [ ] Rotate any real provider API keys used for the live-only checks in §5
- [ ] Reset `AiNarrativeGenerationTimeUtc` to `00:00:00` if changed during testing
