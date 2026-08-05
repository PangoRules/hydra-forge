# Ops Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Answer "if this box reboots, a container OOMs, or the disk fills up, what happens" with "it recovers on its own" instead of "someone notices it's down and SSHes in." Covers restart policies, resource limits, health checks on the two services that don't have one yet, automated Postgres backups with retention, and bounded container log growth.

**Architecture:** All changes are docker-compose/host-script level — no application code changes. Postgres backups run via a host-side cron job calling `docker compose exec postgres pg_dump`, writing timestamped dumps to a host directory with a retention-pruning step in the same script.

**Tech Stack:** docker-compose (`restart`, `deploy.resources`, `healthcheck`, `logging` keys), `pg_dump`/`pg_restore`, cron.

## Global Constraints

- Resource limits must be generous enough not to OOM-kill the app under normal load — this plan sets conservative *floors* based on what's already running (Postgres + MinIO + .NET server + Node/Nuxt + nginx + Hangfire in-process), not aggressive caps; tune down later with real usage data, not guesses.
- Every change must be verified against the actual running stack (`docker compose up -d`), not just "looks right" — docker-compose YAML has enough sharp edges (`deploy.resources` is silently ignored outside Swarm mode unless using the Compose v2 CLI's own resource-limit passthrough) that syntax alone doesn't prove it works.

---

### Task 1: Restart policies

**Files:**
- Modify: `docker-compose.yml` (every service)

**Interfaces:** none.

- [ ] **Step 1: Add `restart: unless-stopped` to every service**

In `docker-compose.yml`, add `restart: unless-stopped` to `minio`, `postgres`, `server`, `web`, and `nginx`. `unless-stopped` (not `always`) is the right choice — it restarts on crash or host reboot, but respects an operator's explicit `docker compose stop <service>` instead of immediately restarting it, which `always` would do.

Leave `searxng` and `ntfy` without it, or use `unless-stopped` too if you actually run those profiles — they're optional (`profiles: [...]`) so an unwanted restart loop matters less, but consistency doesn't hurt. Example for `postgres`:
```yaml
  postgres:
    image: pgvector/pgvector:pg16
    restart: unless-stopped
    environment:
      ...
```

- [ ] **Step 2: Verify a crashed container actually restarts**

```bash
! docker compose up -d
! docker compose kill server
! sleep 5
! docker compose ps server
```
Expected: `server`'s `STATUS` shows `Up X seconds` again (not `Exited`) — `kill` sends SIGKILL, simulating a hard crash, and `restart: unless-stopped` should bring it back without any manual `docker compose up`.

- [ ] **Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(ops): restart services automatically on crash or host reboot"
```

---

### Task 2: Health checks for `server` and `web`, proper `depends_on` gating

**Files:**
- Modify: `docker-compose.yml` (`server` and `web` services)

**Interfaces:**
- Consumes: `GET /api/Health` (server, already exists — `HealthController`) and Nuxt's default root route (web, for a liveness check since there's no dedicated health endpoint on the Nuxt side).

- [ ] **Step 1: Add a healthcheck to the `server` service**

```yaml
  server:
    build:
      context: .
      dockerfile: Dockerfile
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/api/Health"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 30s
```
`curl` must exist inside the `server` image for this `CMD` form to work — the .NET `aspnet` base image doesn't ship it by default. Verify before assuming:
```bash
! docker compose run --rm server which curl
```
If that fails, either add `RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` to the server Dockerfile's final stage, or switch the healthcheck to `CMD-SHELL` with a raw TCP check that doesn't need curl:
```yaml
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/8080 && echo -e 'GET /api/Health HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -q '200' <&3"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 30s
```

- [ ] **Step 2: Add a healthcheck to the `web` service and gate it on `server` being genuinely healthy**

```yaml
  web:
    build:
      context: src/web-ui
      dockerfile: Dockerfile
    restart: unless-stopped
    depends_on:
      server:
        condition: service_healthy
    environment:
      NUXT_API_BASE_URL: http://server:8080
      NUXT_PUBLIC_AUTH_COOKIE_SECURE: ${NUXT_PUBLIC_AUTH_COOKIE_SECURE:-false}
    healthcheck:
      test: ["CMD", "wget", "-q", "-O", "-", "http://localhost:3000/"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 20s
    ports:
      - "3000:3000"
```
This changes `depends_on: server: condition: service_started` (today's setting) to `condition: service_healthy` — `web` now waits for `server`'s `/api/Health` to actually pass, not just for the container process to start, before starting itself. `wget` is present in `node:alpine`-family images by default; verify with `docker compose run --rm web which wget` and swap to `curl` (if present) or a raw Node `http.get` one-liner if it's missing from whichever base image this Dockerfile actually uses.

- [ ] **Step 3: Rebuild and verify the full stack comes up healthy in the right order**

```bash
! docker compose up -d --build
! sleep 45
! docker compose ps
```
Expected: every service shows `(healthy)` next to its status, not `(unhealthy)` or `(health: starting)` after the sleep.

- [ ] **Step 4: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(ops): add health checks to server and web, gate web startup on server health"
```

---

### Task 3: Resource limits

**Files:**
- Modify: `docker-compose.yml` (every service)

**Interfaces:** none.

- [ ] **Step 1: Check the Compose CLI version supports `deploy.resources` outside Swarm**

```bash
! docker compose version
```
Docker Compose v2 (the `docker compose` — no hyphen — CLI) honors `deploy.resources.limits` even without Swarm mode, unlike the legacy `docker-compose` v1 binary. If `docker compose version` reports v2.x, proceed. If this host only has the legacy `docker-compose` v1 binary, use the older top-level `mem_limit`/`cpus` keys instead (shown as a fallback below) since v1 silently ignores `deploy.resources`.

- [ ] **Step 2: Add limits per service (Compose v2 form)**

Add a `deploy.resources.limits` block to each service, sized for a small-to-medium single-box deployment — adjust to the actual host's total RAM/CPU before committing, these are starting floors, not universal constants:

```yaml
  postgres:
    ...
    deploy:
      resources:
        limits:
          memory: 1g
          cpus: "1.0"

  minio:
    ...
    deploy:
      resources:
        limits:
          memory: 512m
          cpus: "0.5"

  server:
    ...
    deploy:
      resources:
        limits:
          memory: 1g
          cpus: "1.5"

  web:
    ...
    deploy:
      resources:
        limits:
          memory: 512m
          cpus: "0.5"

  nginx:
    ...
    deploy:
      resources:
        limits:
          memory: 128m
          cpus: "0.25"
```

If using the legacy v1 binary instead, use this form per service instead of `deploy.resources`:
```yaml
  postgres:
    ...
    mem_limit: 1g
    cpus: 1.0
```

- [ ] **Step 3: Rebuild and verify limits are actually applied**

```bash
! docker compose up -d
! docker stats --no-stream
```
Expected: the `LIMIT` column for each container matches what was configured (not `0B`/no limit).

- [ ] **Step 4: Load-test lightly and confirm nothing gets OOM-killed under normal use**

Use the app normally for a few minutes (browse projects, open a chat, send a message) while watching:
```bash
! docker compose ps
```
Expected: no service shows `Exited (137)` (137 = SIGKILL, the signature of an OOM kill). If one does, raise that service's memory limit and retest — don't just remove the limit.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(ops): set per-service resource limits"
```

---

### Task 4: Bounded container logs

**Files:**
- Modify: `docker-compose.yml` (every service)

**Interfaces:** none.

- [ ] **Step 1: Add a logging driver config to every service**

Docker's default `json-file` log driver has no size cap out of the box — a chatty service can fill the host disk over months of uptime. Add to each service:
```yaml
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"
```
This caps each service at 30MB of retained logs (3 files × 10MB, rotating).

- [ ] **Step 2: Rebuild and verify the option took effect**

```bash
! docker compose up -d
! docker inspect hydra-forge-server-1 --format '{{json .HostConfig.LogConfig}}'
```
Expected: JSON showing `"Type":"json-file"` and `"Config":{"max-size":"10m","max-file":"3"}`.

- [ ] **Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "feat(ops): cap container log growth"
```

---

### Task 5: Automated Postgres backups with retention

**Files:**
- Create: `deploy/postgres-backup.sh`
- Create: `deploy/hydraforge-backup.service`
- Create: `deploy/hydraforge-backup.timer`

**Interfaces:**
- Produces: nightly `pg_dump` snapshots in `/etc/hydraforge/backups/`, pruned to the last 14 days, restorable via `pg_restore` — this is the concrete deliverable this task's steps verify.

- [ ] **Step 1: Write the backup script**

Create `deploy/postgres-backup.sh`:
```bash
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR=/etc/hydraforge/backups
RETENTION_DAYS=14
COMPOSE_FILE=/home/pango/Projects/hydra-forge/docker-compose.yml
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"

mkdir -p "$BACKUP_DIR"

docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_dump -U "${POSTGRES_USER:-hydraforge}" -F custom "${POSTGRES_DB:-hydraforge}" \
  > "$BACKUP_DIR/hydraforge-$TIMESTAMP.dump"

# Prune anything older than RETENTION_DAYS
find "$BACKUP_DIR" -name "hydraforge-*.dump" -mtime "+$RETENTION_DAYS" -delete

echo "Backup complete: $BACKUP_DIR/hydraforge-$TIMESTAMP.dump"
```
`-F custom` (not plain SQL) so it can be restored selectively/in parallel with `pg_restore` rather than only via a full `psql < dump.sql` replay. Adjust the `COMPOSE_FILE` path and `POSTGRES_USER`/`POSTGRES_DB` defaults if they differ from this repo's own `.env.example` defaults on the actual deployment host.

- [ ] **Step 2: Make it executable and run it once by hand**

```bash
! chmod +x deploy/postgres-backup.sh
! deploy/postgres-backup.sh
! ls -lh /etc/hydraforge/backups/
```
Expected: one `.dump` file, non-zero size.

- [ ] **Step 3: Verify the dump is actually restorable**

Restore into a throwaway database to prove the dump isn't corrupt, without touching the real `hydraforge` database:
```bash
! docker compose exec -T postgres createdb -U hydraforge hydraforge_restore_test
! docker compose exec -T postgres pg_restore -U hydraforge -d hydraforge_restore_test < /etc/hydraforge/backups/hydraforge-<TIMESTAMP>.dump
! docker compose exec -T postgres psql -U hydraforge -d hydraforge_restore_test -c "SELECT count(*) FROM users;"
! docker compose exec -T postgres dropdb -U hydraforge hydraforge_restore_test
```
Expected: the `SELECT count(*)` returns a real number matching the live database's user count (not an error) — this is the step that actually proves backups work, not just that the script ran without error.

- [ ] **Step 4: Write the systemd unit and timer**

Create `deploy/hydraforge-backup.service`:
```ini
[Unit]
Description=Nightly HydraForge Postgres backup

[Service]
Type=oneshot
ExecStart=/usr/bin/bash /path/to/hydra-forge/deploy/postgres-backup.sh
```

Create `deploy/hydraforge-backup.timer`:
```ini
[Unit]
Description=Nightly HydraForge Postgres backup schedule

[Timer]
OnCalendar=*-*-* 03:30:00
Persistent=true

[Install]
WantedBy=timers.target
```
`03:30` deliberately sits between the AI-narrative job's and housekeeping's default 03:00 run and typical low-traffic hours — adjust if either of those schedules changes (both are admin-configurable via the Settings page's Nightly Jobs card).

- [ ] **Step 5: Install and enable the timer**

```bash
! sudo cp deploy/hydraforge-backup.service deploy/hydraforge-backup.timer /etc/systemd/system/
! sudo systemctl daemon-reload
! sudo systemctl enable --now hydraforge-backup.timer
! systemctl list-timers hydraforge-backup.timer
```
Expected: shows a `NEXT` time at the next 03:30.

- [ ] **Step 6: Commit**

```bash
git add deploy/postgres-backup.sh deploy/hydraforge-backup.service deploy/hydraforge-backup.timer
git commit -m "feat(ops): automate nightly Postgres backups with 14-day retention"
```

---

## Follow-ups intentionally left out of this plan

- **Off-box backup storage**: Task 5's backups live on the same host as the database — a disk failure or host loss takes both out together. Worth a follow-up to sync `/etc/hydraforge/backups/` to off-box storage (another machine over Tailscale, or S3-compatible storage — MinIO itself is already in this stack but backing up *to* the same MinIO that's part of what you'd be recovering is the same single-point-of-failure problem one layer down).
- **MinIO/attachment backups**: this plan only covers Postgres. The `hydraforge-attachments` Docker volume (or MinIO's own data, depending on `FileStorage__Provider`) isn't backed up by anything here — attachments, gallery images, and document uploads would be lost on data loss even with Postgres restored. Worth a follow-up once off-box backup storage exists for Postgres, extending the same pattern to `docker run --rm -v hydraforge-attachments:/data -v $BACKUP_DIR:/backup alpine tar czf /backup/attachments-$TIMESTAMP.tar.gz -C /data .` (Local provider) or `mc mirror` (S3/MinIO provider).
- **Alerting**: none of this plan pages anyone when a backup fails, a container won't come back healthy, or disk usage climbs despite log rotation. Worth a follow-up once there's a place to send alerts to (ntfy is already in this stack for in-app notifications and could plausibly be reused for ops alerts too).
