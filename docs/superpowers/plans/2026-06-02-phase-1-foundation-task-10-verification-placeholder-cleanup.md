# Task 10: Phase 1 verification and placeholder cleanup
**Branch:** `task/phase-1-verification`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:verification-before-completion, then superpowers:executing-plans.

**Goal:** Verify merged Phase 1 foundation end-to-end and remove leftover scaffolding placeholders that now misrepresent runnable behavior.

**Architecture:** No new feature architecture. This task validates integration after Tasks 1-9 merge into `feat/phase-1-foundation` and cleans only stale placeholders.

**Tech Stack:** .NET build/test, Docker Compose, pnpm, HTTP smoke checks.

---

## Files

- Modify if still present: `tests/HydraForge.Domain.Tests/UnitTest1.cs`
- Modify if still present: `tests/HydraForge.Application.Tests/UnitTest1.cs`
- Modify if still present: `src/HydraForge.Server/Program.cs` weather endpoint remnants
- Modify if needed: `README.md`, `CLAUDE.md` only for commands/conventions changed by real implementation
- Read-only context: all Phase 1 source, tests, Docker/CI files.

## Steps

- [ ] **Step 1: Confirm all prior task branches are merged into parent branch**

```bash
git checkout feat/phase-1-foundation
git pull --ff-only
git log --oneline --decorate -20
```

Expected: recent commits include Tasks 1-9 or merged PR commits.

- [ ] **Step 2: Search for starter placeholders**

Use repo search tools, not blind edits. Confirm whether these remain:

- `/weatherforecast`
- `WeatherForecast`
- `Hello, World!`
- `UnitTest1`

Remove only placeholders replaced by real tests or real server endpoints. Do not remove TUI scaffold unless Phase 1 implementation added a replacement entrypoint.

- [ ] **Step 3: Run .NET verification**

```bash
dotnet restore HydraForge.slnx
dotnet build HydraForge.slnx
dotnet test HydraForge.slnx
```

Expected: restore/build/test pass.

- [ ] **Step 4: Run web verification**

```bash
pnpm install --dir src/web-ui --frozen-lockfile
pnpm --dir src/web-ui lint
pnpm --dir src/web-ui typecheck
pnpm --dir src/web-ui build
```

Expected: lint/typecheck/build pass.

- [ ] **Step 5: Run Docker Compose smoke test**

```bash
cp .env.example .env
docker compose up -d postgres server
docker compose ps
curl -sS http://localhost:8080/health
docker compose down
```

Expected: postgres and server become healthy/running; `/health` returns JSON with server and database components.

- [ ] **Step 6: Verify optional SearXNG profile parses/starts**

```bash
docker compose --profile search config
docker compose --profile search up -d searxng
docker compose --profile search down
```

Expected: config parses and searxng container starts or pulls successfully.

- [ ] **Step 7: Verify auth smoke path**

With server running and `.env` copied from `.env.example` with changed secrets:

```bash
curl -sS -X POST http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"<admin-password-from-env>"}'
```

Expected: JSON response includes access token for enabled seeded admin. Repeat with wrong password and disabled-user test if endpoint/admin API exists; expected failure is ProblemDetails with `correlationId` and named code.

- [ ] **Step 8: Verify logs contain correlation ID**

```bash
curl -sS -H 'X-Correlation-Id: verify-corr-1' http://localhost:8080/health
docker compose logs server
```

Expected: server logs include `verify-corr-1`.

- [ ] **Step 9: Update docs only if commands changed**

If implementation changed setup commands, update `README.md` and `CLAUDE.md` to match working commands. Do not claim production readiness; say local development foundation.

- [ ] **Step 10: Commit task branch**

```bash
git add .
git commit -m "chore: verify phase 1 foundation"
git push
```
