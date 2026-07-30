# Config & Secrets Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove committed dev secrets, add startup validation for JWT signing key and Argon2 parameters, validate NtfyClient URLs, and add forwarded headers middleware.

**Architecture:** Secrets move to env vars with placeholder fallback. Startup validation classes reject unsafe defaults. NtfyClient validates URL scheme before making HTTP calls.

**Tech Stack:** ASP.NET Core 10, .NET user-secrets, Konscious.Security.Cryptography.Argon2

---

### Task 1: Remove Committed Dev Secrets

**Files:**
- Modify: `src/HydraForge.Server/appsettings.Development.json`
- Create: `src/HydraForge.Server/appsettings.Development.example.json`
- Modify: `.gitignore`

- [ ] **Step 1: Replace secrets with placeholder tokens**

Replace `appsettings.Development.json` content:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost:5433;Database=hydraforge;Username=hydraforge;Password=**OVERRIDE-VIA-ENV**"
  },
  "Database": {
    "ApplyMigrationsOnStartup": true
  },
  "FileStorage": {
    "Provider": "S3",
    "LocalPath": ".hydraforge/attachments",
    "MaxBytes": 10485760,
    "S3": {
      "BucketName": "hydraforge-attachments",
      "Region": "us-east-1",
      "AccessKey": "**OVERRIDE-VIA-ENV**",
      "SecretKey": "**OVERRIDE-VIA-ENV**",
      "Endpoint": "http://localhost:9000",
      "ForcePathStyle": true
    }
  },
  "Jwt": {
    "Issuer": "HydraForge",
    "Audience": "HydraForge",
    "SigningKey": "**OVERRIDE-VIA-ENV**",
    "AccessTokenMinutes": 60
  },
  "AdminSeed": {
    "Username": "**OVERRIDE-VIA-ENV**",
    "Password": "**OVERRIDE-VIA-ENV**",
    "Name": "**OVERRIDE-VIA-ENV**",
    "LastName": "**OVERRIDE-VIA-ENV**",
    "Email": "**OVERRIDE-VIA-ENV**"
  },
  "Argon2": {
    "MemorySizeKiB": 65536,
    "Iterations": 3,
    "Parallelism": 4
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": "http://localhost:3000"
  }
}
```

- [ ] **Step 2: Create example config with real dev defaults**

Create `src/HydraForge.Server/appsettings.Development.example.json` with the original content (the one with real dev secrets). This serves as documentation for new developers.

- [ ] **Step 3: Add appsettings.Development.json to .gitignore**

Add to `.gitignore`:

```
# Dev secrets — use appsettings.Development.example.json as template
src/HydraForge.Server/appsettings.Development.json
```

- [ ] **Step 4: Remove tracked file from git index**

```bash
git rm --cached src/HydraForge.Server/appsettings.Development.json
```

- [ ] **Step 5: Set up user-secrets for local dev**

Run:
```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost:5433;Database=hydraforge;Username=hydraforge;Password=hydr4l0c4" --project src/HydraForge.Server
dotnet user-secrets set "Jwt:SigningKey" "5ae089b36a7841843207c603538fa6abe79435b038fa9ca75174c83818d0ea12" --project src/HydraForge.Server
dotnet user-secrets set "FileStorage:S3:AccessKey" "minioadmin" --project src/HydraForge.Server
dotnet user-secrets set "FileStorage:S3:SecretKey" "minioadmin" --project src/HydraForge.Server
dotnet user-secrets set "AdminSeed:Username" "admin" --project src/HydraForge.Server
dotnet user-secrets set "AdminSeed:Password" "admin" --project src/HydraForge.Server
dotnet user-secrets set "AdminSeed:Name" "admin" --project src/HydraForge.Server
dotnet user-secrets set "AdminSeed:LastName" "admin" --project src/HydraForge.Server
dotnet user-secrets set "AdminSeed:Email" "admin@localhost" --project src/HydraForge.Server
```

- [ ] **Step 6: Verify server starts with user-secrets**

Run: `dotnet run --project src/HydraForge.Server`
Expected: Server starts, migrations apply, no config errors.

- [ ] **Step 7: Commit**

```bash
git add src/HydraForge.Server/appsettings.Development.json src/HydraForge.Server/appsettings.Development.example.json .gitignore
git commit -m "fix: move dev secrets to user-secrets, add example config template"
```

---

### Task 2: JWT Signing Key Startup Validation

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`

- [ ] **Step 1: Add placeholder rejection after reading signing key**

In `Program.cs`, after line 86 (the `?? throw` line), add:

```csharp
if (jwtSigningKey == "your-256-bit-secret-key-here-replace-in-production")
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be changed from the default placeholder. " +
        "Set it via environment variable Jwt__SigningKey or user-secrets."
    );
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Server/Program.cs
git commit -m "fix: reject default JWT signing key placeholder at startup"
```

---

### Task 3: Argon2 Parameter Validation

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`

- [ ] **Step 1: Add validation after Argon2 options configuration**

After `builder.Services.Configure<Argon2Options>(...)` (line 89), add:

```csharp
// Validate Argon2 parameters at startup
var argon2Options = builder.Configuration.GetSection("Argon2").Get<Argon2Options>()!;
if (argon2Options.Iterations < 2)
    throw new InvalidOperationException("Argon2:Iterations must be at least 2.");
if (argon2Options.MemorySizeKiB < 32768)
    throw new InvalidOperationException("Argon2:MemorySizeKiB must be at least 32768 (32 MiB).");
if (argon2Options.Parallelism < 1)
    throw new InvalidOperationException("Argon2:Parallelism must be at least 1.");
```

Add `using HydraForge.Infrastructure.Auth;` at top of Program.cs.

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Server/Program.cs
git commit -m "fix: add Argon2 parameter minimum validation at startup"
```

---

### Task 4: NtfyClient URL Validation

**Files:**
- Modify: `src/HydraForge.Infrastructure/Notifications/NtfyClient.cs`

- [ ] **Step 1: Add URL scheme validation**

Replace `PublishAsync` method in `NtfyClient.cs`:

```csharp
public async Task PublishAsync(
    Guid userId,
    string title,
    string? body,
    CancellationToken ct = default
)
{
    var settings = await _settingsProvider.GetAsync(ct);
    if (string.IsNullOrWhiteSpace(settings.NtfyServerUrl))
        return;

    // Validate URL scheme
    if (!Uri.TryCreate(settings.NtfyServerUrl, UriKind.Absolute, out var serverUri))
    {
        return;
    }

    if (serverUri.Scheme != "https" && serverUri.Scheme != "http")
    {
        return;
    }

    // In production, require HTTPS (allow http://localhost for dev)
    if (serverUri.Scheme == "http" && serverUri.Host != "localhost" && serverUri.Host != "127.0.0.1")
    {
        return;
    }

    var topic = $"hydraforge-{userId}";
    var url = $"{settings.NtfyServerUrl.TrimEnd('/')}/{topic}";

    var payload = new
    {
        topic,
        title,
        message = body ?? title,
        priority = _options.DefaultPriority,
        tags = new[] { "hydraforge" },
    };

    try
    {
        await _http.PostAsJsonAsync(url, payload, ct);
    }
    catch
    {
        // ntfy is best-effort — never throw on push failure
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/HydraForge.Infrastructure/Notifications/NtfyClient.cs
git commit -m "fix: validate NtfyClient URL scheme (https required, localhost http allowed)"
```

---

### Task 5: Forwarded Headers Middleware

**Files:**
- Modify: `src/HydraForge.Server/Program.cs`

- [ ] **Step 1: Add forwarded headers configuration**

After `builder.Services.Configure<Argon2Options>(...)`, add:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust the reverse proxy — in production, restrict to known proxy IPs
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

- [ ] **Step 2: Add middleware before authentication**

Before `app.UseAuthentication()`, add:

```csharp
app.UseForwardedHeaders();
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/HydraForge.Server/Program.cs
git commit -m "feat: add forwarded headers middleware for reverse proxy deployments"
```

---

### Verification

Run full verification per `dotnet-verification` skill:
1. `dotnet build` — must pass
2. `dotnet test` — must pass
3. `PATH="$PATH:/home/pango/.dotnet/tools" dotnet ef migrations has-pending-model-changes --project src/HydraForge.Infrastructure --startup-project src/HydraForge.Server` — must report no pending changes
