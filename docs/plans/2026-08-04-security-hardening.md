# Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the concrete gaps found while auditing this deployment for real internet/tailnet-facing use: secrets that ship with no placeholder detection, missing security response headers, containers running as root, and datastores bound to every network interface instead of loopback-only.

**Architecture:** Extend the existing startup-validation pattern already used for `Jwt:SigningKey` (Program.cs throws on the known placeholder) to the other secrets that currently have no such check. Add a small response-header middleware. Add `USER` directives to both Dockerfiles. Rebind Postgres/MinIO host port mappings to `127.0.0.1`.

**Tech Stack:** ASP.NET Core middleware, Docker multi-stage builds, docker-compose port bindings.

## Global Constraints

- Every new startup check must fail fast (throw at boot, not log-and-continue) — this repo's existing convention for `Jwt:SigningKey` (`Program.cs:187-201`) is the model to match exactly.
- Don't weaken anything that already works — this plan only adds checks/headers/isolation, it doesn't change existing auth, CORS, or rate-limit behavior.
- `.env.example` values are documentation, not real secrets — every placeholder value referenced in code must be copied verbatim from the actual current `.env.example`, not paraphrased.

---

### Task 1: Reject known-placeholder secrets at startup

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`
- Test: `tests/HydraForge.Server.Tests/Startup/PlaceholderSecretValidationTests.cs` (new)

**Interfaces:**
- Produces: the app refuses to boot (`InvalidOperationException`, same as the existing `Jwt:SigningKey` check) if `AdminSeed:Password`, `Llm:EncryptionKey`, or the Postgres connection string's password segment still match their shipped `.env.example` values.

- [ ] **Step 1: Read the current placeholder values verbatim**

```bash
! grep -E "AdminSeed__Password|Llm__EncryptionKey|POSTGRES_PASSWORD" /home/pango/Projects/hydra-forge/.env.example
```
Use exactly what this prints in the steps below — don't retype from memory.

- [ ] **Step 2: Write the failing test**

Create `tests/HydraForge.Server.Tests/Startup/PlaceholderSecretValidationTests.cs`:
```csharp
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests.Startup;

public class PlaceholderSecretValidationTests
{
    [Fact]
    public void Startup_WithPlaceholderAdminSeedPassword_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Test");
                builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
                builder.UseSetting("Llm:EncryptionKey", "0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=");
                builder.UseSetting("AdminSeed:Username", "admin");
                builder.UseSetting("AdminSeed:Password", "change-this-admin-password");
            });
            using var client = factory.CreateClient();
        });

        Assert.Contains("AdminSeed:Password", ex.Message);
    }

    [Fact]
    public void Startup_WithPlaceholderLlmEncryptionKey_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Environment", "Test");
                builder.UseSetting("Jwt:SigningKey", "test-secret-key-that-is-at-least-32-chars-long-for-hs256");
                builder.UseSetting("Llm:EncryptionKey", "ckaOH71rTlfT6cR0r28AObevMLQSJFCz4goqiV2aMwY=");
                builder.UseSetting("AdminSeed:Username", "admin");
                builder.UseSetting("AdminSeed:Password", "a-real-generated-password-not-the-example");
            });
            using var client = factory.CreateClient();
        });

        Assert.Contains("Llm:EncryptionKey", ex.Message);
    }
}
```
Note: this deliberately doesn't reuse `AdminTestWebApplicationFactory` or any other existing test factory — those factories already override config to non-placeholder test values specifically to boot successfully, which is the opposite of what this test needs.

- [ ] **Step 2: Run it to confirm it fails for the right reason first**

```bash
! dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~PlaceholderSecretValidationTests"
```
Expected: both tests FAIL — not because of a compile error, but because the app currently boots successfully with these placeholder values (no exception thrown).

- [ ] **Step 3: Add the checks to `Program.cs`**

Immediately after the existing `Jwt:SigningKey` placeholder block (`Program.cs`, right after the `if (jwtSigningKey.Length < 32)` check, before `var accessTokenMinutes = ...`), add:

```csharp
// AdminSeed:Password — mirrors the Jwt:SigningKey check above. Without this, a
// deployment that never edited .env.example's AdminSeed__Password ships an admin
// account whose password is public in this repo's git history.
var adminSeedPassword = builder.Configuration["AdminSeed:Password"];
if (adminSeedPassword == "change-this-admin-password")
{
    throw new InvalidOperationException(
        "AdminSeed:Password must be changed from the default placeholder. "
            + "Set it via environment variable AdminSeed__Password or user-secrets."
    );
}

// Llm:EncryptionKey — AddLlmInfrastructure already requires this to be present and
// well-formed (missing/invalid → InvalidOperationException at startup per its own
// validation), but it does not reject the specific key .env.example ships, which is
// syntactically valid and would pass that check silently.
var llmEncryptionKey = builder.Configuration["Llm:EncryptionKey"];
if (llmEncryptionKey == "ckaOH71rTlfT6cR0r28AObevMLQSJFCz4goqiV2aMwY=")
{
    throw new InvalidOperationException(
        "Llm:EncryptionKey must be changed from the default placeholder shipped in "
            + ".env.example. Generate your own with: openssl rand -base64 32"
    );
}
```

- [ ] **Step 4: Run the tests again to confirm they pass**

```bash
! dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~PlaceholderSecretValidationTests"
```
Expected: PASS.

- [ ] **Step 5: Run the full Server.Tests suite to confirm nothing else broke**

```bash
! dotnet test tests/HydraForge.Server.Tests
```
Expected: all passing, same count as before this task (every other test factory already sets non-placeholder values for these two settings — see CLAUDE.md's "Llm:EncryptionKey in test factories" note).

- [ ] **Step 6: Commit**

```bash
git add src/HydraForge.Server/Program.cs tests/HydraForge.Server.Tests/Startup/PlaceholderSecretValidationTests.cs
git commit -m "fix(security): reject known-placeholder AdminSeed password and LLM encryption key at startup"
```

---

### Task 2: Security response headers

**Files:**
- Create: `src/HydraForge.Server/Middleware/SecurityHeadersMiddleware.cs`
- Modify: `src/HydraForge.Server/Program.cs`
- Test: `tests/HydraForge.Server.Tests/Middleware/SecurityHeadersMiddlewareTests.cs` (new)

**Interfaces:**
- Produces: every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`. `Strict-Transport-Security` is added only when the request arrived over HTTPS (`context.Request.IsHttps`, which reflects `X-Forwarded-Proto` once `UseForwardedHeaders()` — already registered ahead of this middleware — has run) — sending HSTS over a plain-HTTP deployment that hasn't done the TLS plan yet would be actively harmful (it tells the browser to *refuse* future plain-HTTP connections to this host).

- [ ] **Step 1: Write the failing test**

Create `tests/HydraForge.Server.Tests/Middleware/SecurityHeadersMiddlewareTests.cs`:
```csharp
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HydraForge.Server.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Response_AlwaysIncludesBaselineSecurityHeaders()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Test");
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal(
            "strict-origin-when-cross-origin",
            response.Headers.GetValues("Referrer-Policy").Single()
        );
    }

    [Fact]
    public async Task Response_OverPlainHttp_DoesNotIncludeHsts()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Test");
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/Health");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
```
Add `using System.Linq;` if `Single()` doesn't resolve — check the existing usings convention in a neighboring test file in the same directory first.

- [ ] **Step 2: Run it to confirm it fails**

```bash
! dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~SecurityHeadersMiddlewareTests"
```
Expected: FAIL — `X-Content-Type-Options` etc. are not present on responses today.

- [ ] **Step 3: Write the middleware**

Create `src/HydraForge.Server/Middleware/SecurityHeadersMiddleware.cs`:
```csharp
namespace HydraForge.Server.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Only over HTTPS — sending this over plain HTTP tells the browser to
            // refuse future plain-HTTP connections to this host, which would lock
            // out any deployment that hasn't done the TLS plan yet.
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
```

- [ ] **Step 4: Register it in `Program.cs`**

In `Program.cs`, add the middleware registration right after `app.UseForwardedHeaders();` (so `context.Request.IsHttps` already reflects `X-Forwarded-Proto` from nginx) and before `app.UseSerilogRequestLogging(...)`:
```csharp
app.UseForwardedHeaders();

app.UseMiddleware<HydraForge.Server.Middleware.SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging(options =>
```

- [ ] **Step 5: Run the tests again to confirm they pass**

```bash
! dotnet test tests/HydraForge.Server.Tests --filter "FullyQualifiedName~SecurityHeadersMiddlewareTests"
```
Expected: PASS.

- [ ] **Step 6: Run the full Server.Tests suite**

```bash
! dotnet test tests/HydraForge.Server.Tests
```
Expected: all passing.

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Server/Middleware/SecurityHeadersMiddleware.cs src/HydraForge.Server/Program.cs tests/HydraForge.Server.Tests/Middleware/SecurityHeadersMiddlewareTests.cs
git commit -m "feat(security): add baseline security response headers, HSTS when served over HTTPS"
```

---

### Task 3: Non-root containers

**Files:**
- Modify: `Dockerfile` (server)
- Modify: `src/web-ui/Dockerfile`

**Interfaces:** none — this task only changes the container's runtime user, no application interface changes.

- [ ] **Step 1: Read both Dockerfiles fully before editing**

```bash
! cat /home/pango/Projects/hydra-forge/Dockerfile
! cat /home/pango/Projects/hydra-forge/src/web-ui/Dockerfile
```
Identify the final stage of each (the one that actually runs, not the build stage) — that's the only stage needing a `USER` directive. Note the exact base image (e.g. `mcr.microsoft.com/dotnet/aspnet:10.0` or `node:XX-alpine`) since the fix differs slightly by base image family.

- [ ] **Step 2: Add a non-root user to the server Dockerfile's final stage**

.NET's `aspnet` base images ship a built-in unprivileged `app` user (uid 1654 in recent images) — prefer that over creating a new one if the base image has it:
```dockerfile
USER app
```
Add this line in the final stage, after the `COPY --from=build ...` steps and before the `ENTRYPOINT`/`CMD`. If the base image doesn't ship an `app` user (check with `docker compose run --rm server id app` before assuming), add instead:
```dockerfile
RUN useradd --uid 1000 --user-group --no-create-home appuser
USER appuser
```

- [ ] **Step 3: Add a non-root user to the web-ui Dockerfile's final stage**

Node's official images ship a built-in `node` user:
```dockerfile
USER node
```
Add after the final `COPY` steps, before `CMD`.

- [ ] **Step 4: Rebuild and verify**

```bash
! docker compose up -d --build server web
! docker compose exec server id
! docker compose exec web id
```
Expected: neither shows `uid=0(root)`.

- [ ] **Step 5: Verify the app still boots and serves correctly**

```bash
! curl -s http://localhost:8080/api/Health
```
Expected: a 200 JSON health response — confirms the app didn't fail to start under the new unprivileged user (a common breakage mode: the app tries to write somewhere the new user can't, e.g. a bind-mounted volume owned by root — check `FileStorage__LocalPath` and the `hydraforge-attachments` volume's ownership if this fails).

- [ ] **Step 6: Commit**

```bash
git add Dockerfile src/web-ui/Dockerfile
git commit -m "fix(security): run server and web containers as a non-root user"
```

---

### Task 4: Bind Postgres and MinIO to loopback only

**Files:**
- Modify: `docker-compose.yml`

**Interfaces:** none — internal container-to-container traffic (server → postgres, server → minio) goes over the docker-compose bridge network regardless of host port bindings, so this task only affects what's reachable from outside the host.

- [ ] **Step 1: Change the port mappings**

In `docker-compose.yml`, change the `postgres` service's port mapping from:
```yaml
    ports:
      - "${POSTGRES_PORT:-5433}:5432"
```
to:
```yaml
    ports:
      - "127.0.0.1:${POSTGRES_PORT:-5433}:5432"
```

And the `minio` service's from:
```yaml
    ports:
      - "${MINIO_PORT:-9000}:9000"
      - "${MINIO_CONSOLE_PORT:-9001}:9001"
```
to:
```yaml
    ports:
      - "127.0.0.1:${MINIO_PORT:-9000}:9000"
      - "127.0.0.1:${MINIO_CONSOLE_PORT:-9001}:9001"
```

This preserves the existing "connect with a local Postgres/MinIO client for debugging" workflow (still reachable from the host itself via `localhost:5433` / `localhost:9001`) while removing reachability from the rest of the Tailscale network or LAN — today, anyone who can reach this host's Tailscale IP can attempt a Postgres connection directly using the password from `.env`, entirely bypassing the application and its auth/rate-limiting.

- [ ] **Step 2: Recreate the containers and verify loopback-only binding**

```bash
! docker compose up -d postgres minio
! docker compose port postgres 5432
! docker compose port minio 9000
```
Expected output shows `127.0.0.1:5433` and `127.0.0.1:9000` (not `0.0.0.0:...`).

- [ ] **Step 3: Verify the app itself still connects fine (it uses the internal docker network, not these host bindings)**

```bash
! curl -s http://localhost:8080/api/Health
```
Expected: 200, with the database probe reporting healthy — confirms `server`'s connection to `postgres` (via `Host=postgres;Port=5432` on the internal network) is unaffected by this change.

- [ ] **Step 4: Verify external reachability is actually closed**

From a *different* machine on the same Tailscale network (not the Docker host itself):
```bash
! nc -zv -w3 <TAILNET_HOSTNAME_OR_IP> 5433
```
Expected: connection refused/timeout (previously this would have succeeded).

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "fix(security): bind Postgres and MinIO host ports to loopback only"
```

---

### Task 5: Set the deployment default to `ASPNETCORE_ENVIRONMENT=Production`

**Files:**
- Modify: `.env.example`
- Modify: `docker-compose.yml` (`server` service environment)

**Interfaces:**
- Consumes: `app.Environment.IsDevelopment()` checks already in `Program.cs` (Scalar/OpenAPI mapping at line 346-350, `TestUserSeeder` at line 369-373) — this task doesn't touch that code, only ensures the env var reaching it is `Production` by default for anyone following `.env.example`.

- [ ] **Step 1: Update `.env.example`**

Change:
```
ASPNETCORE_ENVIRONMENT=Development
```
to:
```
# Production disables the Scalar API reference UI, OpenAPI JSON endpoint, and the
# Development-only test user seeder (testadmin/testuser1/nonmember). Set to
# Development only for local `dotnet run` work against this repo's source.
ASPNETCORE_ENVIRONMENT=Production
```

- [ ] **Step 2: Confirm `docker-compose.yml`'s `server` service doesn't hardcode an override**

```bash
! grep -n "ASPNETCORE_ENVIRONMENT" /home/pango/Projects/hydra-forge/docker-compose.yml
```
If this prints nothing, the `server` service inherits `ASPNETCORE_ENVIRONMENT` from `env_file: .env` already (per the service's existing `env_file: - .env` directive) — no docker-compose change needed, skip to Step 3. If it prints a hardcoded `Development` value, change it to read from `.env` the same way `NUXT_PUBLIC_AUTH_COOKIE_SECURE` was fixed in the HTTPS plan's Task 3.

- [ ] **Step 3: Rebuild and verify Development-only surfaces are gone**

```bash
! docker compose up -d --build server
! curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/scalar/v1
! curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/openapi/v1.json
```
Expected: both return `404` (not `200`) — confirms `IsDevelopment()` correctly gated them off under `Production`.

- [ ] **Step 4: Verify login still works with the real admin account (not the Development-only test users)**

```bash
! curl -s -X POST http://localhost:8080/api/Auth/login -H "Content-Type: application/json" -d '{"username":"<your-admin-username>","password":"<your-real-admin-password>"}' -o /dev/null -w "%{http_code}"
```
Expected: `200`. If you were relying on `testadmin`/`TestAdmin123!` for local testing, note those credentials no longer work under `Production` — that's the intended effect of this task, not a regression.

- [ ] **Step 5: Commit**

```bash
git add .env.example docker-compose.yml
git commit -m "fix(security): default deployment to ASPNETCORE_ENVIRONMENT=Production"
```

---

## Follow-ups intentionally left out of this plan

- **Content-Security-Policy header**: deliberately not added in Task 2 — a CSP tight enough to be worth anything needs to enumerate every script/style/connect source the Nuxt app actually uses (including inline styles Tailwind/Nuxt UI may inject, and the SignalR WebSocket origin), and getting it wrong silently breaks the UI rather than failing loudly. Worth a dedicated follow-up plan with the Web UI running locally to iterate against `Content-Security-Policy-Report-Only` before enforcing it.
- **Postgres/MinIO credential rotation**: this plan hardens *where* the secrets can be reached from, not what they currently are. If `.env`'s `POSTGRES_PASSWORD`/`MINIO_ROOT_PASSWORD`/`AdminSeed__Password` have ever been the `.env.example` placeholders in a real deployment, rotate them by hand (`openssl rand -base64 24`) after this plan lands — Task 1 only catches `AdminSeed:Password` and `Llm:EncryptionKey` going forward, it can't detect an already-seeded admin account that used the placeholder before this check existed.
