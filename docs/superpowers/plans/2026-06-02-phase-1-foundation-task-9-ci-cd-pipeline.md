# Task 9: CI/CD pipeline
**Branch:** `task/phase-1-ci`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans.

**Goal:** Add GitHub Actions workflow for .NET restore/build/test and web UI install/lint/typecheck/build.

**Architecture:** CI verifies existing project boundaries without starting app services unless tests need PostgreSQL. Web UI remains separate pnpm package under `src/web-ui`.

**Tech Stack:** GitHub Actions, .NET 10, pnpm 11.5.0, Nuxt 4 package currently in repo.

---

## Files

- Create: `.github/workflows/ci.yml`
- Read-only context: `HydraForge.slnx`, all csproj files, `src/web-ui/package.json`, `src/web-ui/pnpm-lock.yaml`, `src/web-ui/nuxt.config.ts`, `src/web-ui/eslint.config.mjs`

## Steps

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

Use one workflow with two jobs: `dotnet` and `web-ui`.

```yaml
name: CI

on:
  push:
    branches: [main, "feat/**", "task/**"]
  pull_request:
    branches: [main, "feat/**"]

jobs:
  dotnet:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Cache NuGet
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
      - name: Restore
        run: dotnet restore HydraForge.slnx
      - name: Build
        run: dotnet build HydraForge.slnx --configuration Release --no-restore
      - name: Test
        run: dotnet test HydraForge.slnx --configuration Release --no-build

  web-ui:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/web-ui
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
        with:
          version: 11.5.0
      - uses: actions/setup-node@v4
        with:
          node-version: "22"
          cache: pnpm
          cache-dependency-path: src/web-ui/pnpm-lock.yaml
      - name: Install dependencies
        run: pnpm install --frozen-lockfile
      - name: Lint
        run: pnpm lint
      - name: Typecheck
        run: pnpm typecheck
      - name: Build
        run: pnpm build
```

- [ ] **Step 2: Validate workflow syntax locally if `act` exists**

```bash
command -v act >/dev/null && act --list || true
```

Expected: no required local validation if `act` is absent; command exits successfully.

- [ ] **Step 3: Run matching local commands**

```bash
dotnet restore HydraForge.slnx
dotnet build HydraForge.slnx
dotnet test HydraForge.slnx
```

Then:

```bash
pnpm install --dir src/web-ui --frozen-lockfile
pnpm --dir src/web-ui lint
pnpm --dir src/web-ui typecheck
pnpm --dir src/web-ui build
```

Expected: commands pass in current repo state after prior tasks merge.

- [ ] **Step 4: Commit task branch**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add build and test workflow"
git push
```
