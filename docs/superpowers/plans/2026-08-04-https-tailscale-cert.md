# HTTPS via Tailscale Cert Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serve HydraForge over real HTTPS on the tailnet, using a Tailscale-issued Let's Encrypt cert, so the browser grants secure-context APIs (the class of bug that broke mobile chat), unlocks HTTP/2 (removes the ~6-connections-per-origin cap that made SignalR handshakes starve REST calls), and lets auth cookies actually be `Secure`.

**Architecture:** `tailscaled` runs on the Docker host (not containerized) and is the only thing that can call `tailscale cert` — cert files land on the host filesystem and get bind-mounted read-only into the `nginx` container. nginx terminates TLS on 443 and reverse-proxies exactly as it does today; the plain-80 listener becomes a 301-to-443 redirect. A host-side systemd timer re-issues the cert weekly (Tailscale/Let's Encrypt certs are short-lived) and reloads nginx after.

**Tech Stack:** Tailscale CLI (`tailscale cert`), nginx TLS termination, systemd (timer + service, host-level, not containerized), existing docker-compose stack.

## Global Constraints

- Do not commit cert/key files to git — they're host-local, generated, and rotate.
- Do not remove the plain-HTTP path entirely — port 80 must still answer (with a redirect), since some tooling (health checks, `curl` scripts) may hit it by IP without HTTPS during transition.
- `NUXT_PUBLIC_AUTH_COOKIE_SECURE` and `Cors:AllowedOrigins` must be flipped together — a secure cookie with an http-origin CORS entry (or vice versa) breaks auth silently.
- Every step must be verifiable by the operator running this once, by hand, against their real tailnet — there is no CI for this plan.

---

### Task 1: Determine the tailnet hostname and issue the first cert by hand

**Files:** none (discovery + one-time manual command, confirms the mechanism works before automating it)

**Interfaces:**
- Produces: the exact MagicDNS hostname (e.g. `myhost.tailxxxxx.ts.net`) that every later task in this plan substitutes for `<TAILNET_HOSTNAME>`.

- [ ] **Step 1: Confirm Tailscale HTTPS certs are enabled for this tailnet**

Run on the host:
```bash
! tailscale status
```
Note the hostname shown for this machine (the `<name>` before `.<tailnet>.ts.net`). If HTTPS certs aren't enabled yet, enable them in the Tailscale admin console: **DNS → HTTPS Certificates → Enable HTTPS**. This is a one-time account-level toggle, not something the CLI can do.

- [ ] **Step 2: Issue a cert by hand to confirm the mechanism works**

```bash
! sudo mkdir -p /etc/hydraforge/tailscale-certs
! sudo tailscale cert --cert-file /etc/hydraforge/tailscale-certs/tls.crt --key-file /etc/hydraforge/tailscale-certs/tls.key <TAILNET_HOSTNAME>
```
Expected: two files created, no error. `sudo` is required — `tailscale cert` needs to read the node's Tailscale identity state, which is root-owned.

- [ ] **Step 3: Verify the cert contents**

```bash
! openssl x509 -in /etc/hydraforge/tailscale-certs/tls.crt -noout -subject -dates
```
Expected: `subject=CN = <TAILNET_HOSTNAME>` and a `notAfter` date roughly 90 days out (Let's Encrypt default lifetime via Tailscale).

- [ ] **Step 4: Lock down permissions**

```bash
! sudo chmod 600 /etc/hydraforge/tailscale-certs/tls.key
! sudo chmod 644 /etc/hydraforge/tailscale-certs/tls.crt
```

No commit — this task only touches the host filesystem outside the repo.

---

### Task 2: nginx TLS termination

**Files:**
- Modify: `nginx/nginx.conf.template`
- Modify: `docker-compose.yml` (nginx service: port mapping + cert volume mount)

**Interfaces:**
- Consumes: `/etc/hydraforge/tailscale-certs/tls.crt` and `tls.key` from Task 1, bind-mounted into the container at `/etc/nginx/certs/`.
- Produces: nginx listening on 443 with TLS, port 80 redirecting to 443. Every existing `location` block (`/api/`, `/hubs/`, `/hangfire`, `/`) is unchanged in content, just moved under the new TLS `server` block.

- [ ] **Step 1: Add the cert volume mount and 443 port mapping**

In `docker-compose.yml`, update the `nginx` service (currently around line 99-114):

```yaml
  nginx:
    image: nginx:alpine
    ports:
      - "8080:80"
      - "8443:443"
    environment:
      BACKEND_HOST: ${BACKEND_HOST:-server:8080}
      NUXT_HOST: ${NUXT_HOST:-web:3000}
    volumes:
      - ./nginx/nginx.conf.template:/etc/nginx/templates/default.conf.template:ro
      - /etc/hydraforge/tailscale-certs:/etc/nginx/certs:ro
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

Kept the host port at 8443 (not the default 443) for the same reason the existing `8080` mapping exists — leaves the bare hostname free for other services on the box. Adjust if you'd rather have this be the box's only HTTPS service on 443.

- [ ] **Step 2: Split `nginx.conf.template` into an HTTP-redirect server and an HTTPS server**

Replace the single `server { listen 80; ... }` block in `nginx/nginx.conf.template` with:

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

upstream backend {
    server ${BACKEND_HOST};
}

upstream nuxt {
    server ${NUXT_HOST};
}

server {
    listen 80;
    server_name _;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    http2 on;
    server_name _;

    ssl_certificate     /etc/nginx/certs/tls.crt;
    ssl_certificate_key /etc/nginx/certs/tls.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Nuxt Icon's own runtime icon-fetch API — must win over the /api/ block
    # below (longest-prefix match) since it lives under /api/ but is served
    # by Nuxt itself, not the .NET backend. Without this, any icon not in the
    # client-bundled sprite 404s (misrouted to the backend, which has no such
    # route) instead of being fetched from Nuxt.
    location /api/_nuxt_icon/ {
        proxy_pass http://nuxt;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # REST API — direct to .NET, no hop through Nuxt
    location /api/ {
        proxy_pass http://backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR hubs — WebSocket + SSE + long-polling all work natively
    location /hubs/ {
        proxy_pass http://backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400s;
        proxy_buffering off;
    }

    # Hangfire dashboard
    location /hangfire {
        proxy_pass http://backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Nuxt frontend — pass through WebSocket headers for HMR in dev builds
    location / {
        proxy_pass http://nuxt;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

`http2 on;` is the nginx 1.25+ directive form (replaces the older `listen 443 ssl http2;` syntax) — confirm the `nginx:alpine` tag in use is recent enough with `docker compose exec nginx nginx -v`; if it predates 1.25, use `listen 443 ssl http2;` instead and drop the separate `http2 on;` line.

- [ ] **Step 3: Rebuild and verify the TLS handshake**

```bash
! docker compose up -d --build nginx
! curl -vk https://<TAILNET_HOSTNAME>:8443/api/Health 2>&1 | grep -E "SSL connection|subject:|HTTP/"
```
Expected: `SSL connection using TLSv1.3`, `subject: CN=<TAILNET_HOSTNAME>`, and an HTTP 200 from `/api/Health`. Also confirm the redirect:
```bash
! curl -sI http://<TAILNET_HOSTNAME>:8080/ | head -3
```
Expected: `HTTP/1.1 301 Moved Permanently` with a `Location: https://...` header.

- [ ] **Step 4: Commit**

```bash
git add nginx/nginx.conf.template docker-compose.yml
git commit -m "feat(deploy): terminate TLS in nginx with a Tailscale-issued cert"
```

---

### Task 3: Flip cookie/CORS config to HTTPS and verify auth end-to-end

**Files:**
- Modify: `.env` (operator's real file, not `.env.example` — see Step 1)
- Modify: `.env.example` (document the HTTPS values as the new recommended default)
- Modify: `docker-compose.yml` (`web` service environment block)

**Interfaces:**
- Consumes: `<TAILNET_HOSTNAME>` from Task 1, port 8443 from Task 2.
- Produces: `NUXT_PUBLIC_AUTH_COOKIE_SECURE=true` and `Cors:AllowedOrigins` containing the `https://` origin — the pairing CLAUDE.md and this plan's Global Constraints call out as needing to move together.

- [ ] **Step 1: Update the operator's `.env`**

Add or update these lines in `.env` (create it from `.env.example` first if it doesn't exist yet):
```
CORS_ALLOWED_ORIGINS=https://<TAILNET_HOSTNAME>:8443
NUXT_PUBLIC_AUTH_COOKIE_SECURE=true
```

- [ ] **Step 2: Update `web` service's `NUXT_PUBLIC_AUTH_COOKIE_SECURE` default in `docker-compose.yml`**

Find the `web` service's `environment:` block (currently has `NUXT_PUBLIC_AUTH_COOKIE_SECURE: "false"` hardcoded, not reading from `.env`). Change it to read from the environment so `.env`'s value actually takes effect:
```yaml
  web:
    build:
      context: src/web-ui
      dockerfile: Dockerfile
    depends_on:
      server:
        condition: service_started
    environment:
      NUXT_API_BASE_URL: http://server:8080
      NUXT_PUBLIC_AUTH_COOKIE_SECURE: ${NUXT_PUBLIC_AUTH_COOKIE_SECURE:-false}
    ports:
      - "3000:3000"
```
Defaulting to `false` here preserves today's plain-HTTP behavior for anyone who hasn't set up TLS yet — only an explicit `.env` value flips it.

- [ ] **Step 3: Update `.env.example`'s documentation**

In `.env.example`, update the CORS and auth-cookie comment blocks (currently near the bottom) to show the HTTPS values as the recommended path once this plan has been run, keeping the old plain-HTTP values as the fallback/dev comment:
```
# CORS: comma-separated allowed origins for the .NET backend.
# Plain HTTP (no TLS set up yet): http://localhost, or your Tailscale IP e.g. http://100.x.x.x:8080
# Once HTTPS is set up (docs/superpowers/plans/2026-08-04-https-tailscale-cert.md): https://<your-tailnet-hostname>:8443
CORS_ALLOWED_ORIGINS=http://localhost

# Auth cookie settings
NUXT_PUBLIC_AUTH_COOKIE_MAX_AGE=3600
# Must be "true" once served over HTTPS — a Secure cookie is silently dropped by the
# browser over plain HTTP, and a non-Secure cookie over HTTPS is an avoidable downgrade.
NUXT_PUBLIC_AUTH_COOKIE_SECURE=false
```

- [ ] **Step 4: Rebuild and verify login works end-to-end over HTTPS**

```bash
! docker compose up -d --build server web nginx
```
Then from a browser (desktop is fine for this check — mobile-specific verification is Task 4), visit `https://<TAILNET_HOSTNAME>:8443/login`, log in, and open devtools → Application/Storage → Cookies. Confirm the `auth_token` cookie has the `Secure` flag checked. Confirm the page loads with no CORS errors in the console.

- [ ] **Step 5: Commit**

```bash
git add .env.example docker-compose.yml
git commit -m "feat(deploy): default auth cookie to Secure and document HTTPS CORS origin"
```

(`.env` itself is gitignored — not part of this commit.)

---

### Task 4: Verify on mobile and set up cert auto-renewal

**Files:**
- Create: `deploy/tailscale-cert-renew.sh`
- Create: `deploy/hydraforge-cert-renew.service`
- Create: `deploy/hydraforge-cert-renew.timer`
- Modify: `docs/scope.md` or wherever deployment docs live — add a short "HTTPS setup" pointer to this plan (check `docs/` for the right existing file before creating a new one)

**Interfaces:**
- Consumes: the mount path `/etc/hydraforge/tailscale-certs` from Task 1/2.
- Produces: a systemd timer that re-issues the cert weekly and reloads nginx — no manual renewal ever required again.

- [ ] **Step 1: Verify on the actual phone that broke before**

On the mobile device: navigate to `https://<TAILNET_HOSTNAME>:8443/chats`, log in, open (or start) a chat, send a message. Expected: it works — this is the same device/network path that hit the `crypto.randomUUID` secure-context bug, now genuinely fixed at the transport level rather than worked around. If you have remote-debugging still set up from earlier, watch the Network tab once more for confirmation, but it shouldn't be necessary this time.

- [ ] **Step 2: Write the renewal script**

Create `deploy/tailscale-cert-renew.sh`:
```bash
#!/usr/bin/env bash
set -euo pipefail

CERT_DIR=/etc/hydraforge/tailscale-certs
HOSTNAME="${1:?Usage: tailscale-cert-renew.sh <tailnet-hostname>}"

tailscale cert --cert-file "$CERT_DIR/tls.crt" --key-file "$CERT_DIR/tls.key" "$HOSTNAME"
chmod 600 "$CERT_DIR/tls.key"
chmod 644 "$CERT_DIR/tls.crt"

# tailscale cert is a no-op (exits 0, doesn't rewrite files) if the existing
# cert still has plenty of validity left, so this is safe to run on a
# schedule far more often than the cert actually needs renewing.
docker compose -f /home/pango/Projects/hydra-forge/docker-compose.yml exec -T nginx nginx -s reload
```
Adjust the docker-compose path in the last line to match wherever this repo actually lives on the deployment host if it differs.

- [ ] **Step 3: Make it executable and test it manually once**

```bash
! chmod +x deploy/tailscale-cert-renew.sh
! sudo deploy/tailscale-cert-renew.sh <TAILNET_HOSTNAME>
```
Expected: exits 0, nginx reload logged (`docker compose logs nginx --tail 5` should show a "signal process started" or similar reload line, not an error).

- [ ] **Step 4: Write the systemd unit and timer**

Create `deploy/hydraforge-cert-renew.service`:
```ini
[Unit]
Description=Renew HydraForge's Tailscale-issued TLS cert and reload nginx

[Service]
Type=oneshot
ExecStart=/usr/bin/bash /path/to/hydra-forge/deploy/tailscale-cert-renew.sh <TAILNET_HOSTNAME>
```

Create `deploy/hydraforge-cert-renew.timer`:
```ini
[Unit]
Description=Weekly HydraForge TLS cert renewal check

[Timer]
OnCalendar=weekly
Persistent=true

[Install]
WantedBy=timers.target
```

`Persistent=true` means if the host was off when the weekly slot passed, it runs once on next boot instead of waiting a full week — matters for a machine that isn't always on.

- [ ] **Step 5: Install and enable the timer**

```bash
! sudo cp deploy/hydraforge-cert-renew.service deploy/hydraforge-cert-renew.timer /etc/systemd/system/
! sudo systemctl daemon-reload
! sudo systemctl enable --now hydraforge-cert-renew.timer
! systemctl list-timers hydraforge-cert-renew.timer
```
Expected: the timer shows up with a `NEXT` time roughly a week out.

- [ ] **Step 6: Document the setup**

Add a short section to whichever `docs/` file covers deployment (check `docs/scope.md` and `docs/architecture.md` first — follow this repo's existing doc-organization convention rather than creating a new top-level doc) pointing at this plan file for the full HTTPS setup steps, and noting the systemd timer now handles renewal automatically.

- [ ] **Step 7: Commit**

```bash
git add deploy/ docs/
git commit -m "feat(deploy): automate Tailscale cert renewal via systemd timer"
```
