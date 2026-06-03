# Task 1: Docker Compose and environment template
**Branch:** `task/phase-1-docker-env`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make local development stack runnable with PostgreSQL 16, server container wiring, optional SearXNG profile, and copyable env template.

**Architecture:** Keep config at composition root. Server reads env/config; Docker only supplies values. No app feature behavior lands in this task.

**Tech Stack:** Docker Compose, PostgreSQL 16 with pgvector image, ASP.NET Core server container, `.env` variables.

---

## Files

- Modify: `docker-compose.yml` (currently empty)
- Modify: `.env.example` (currently empty)
- Create: `Dockerfile`
- Read-only context: `src/HydraForge.Server/HydraForge.Server.csproj`, `src/HydraForge.Server/Program.cs`, `HydraForge.slnx`

## Steps

- [ ] **Step 1: Create server Dockerfile at repo root**

Use .NET 10 SDK/runtime images, copy solution + source projects, restore `HydraForge.slnx`, publish `HydraForge.Server`, run as non-root if image supports it.

Expected shape:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HydraForge.slnx ./
COPY src ./src
RUN dotnet restore HydraForge.slnx
RUN dotnet publish src/HydraForge.Server/HydraForge.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "HydraForge.Server.dll"]
```

- [ ] **Step 2: Fill `.env.example` with local-dev values**

Use strong example names but mark secrets for replacement.

```dotenv
POSTGRES_DB=hydraforge
POSTGRES_USER=hydraforge
POSTGRES_PASSWORD=change-this-postgres-password
# Host-side port — mapped to container 5432. Using 5433 to avoid conflict with any
# local PostgreSQL or Odysseus (which runs on the same machine).
POSTGRES_PORT=5433

ASPNETCORE_ENVIRONMENT=Development
# Container-internal port — host mapping is 5000:8080 (see docker-compose.yml).
# 8080 is taken by Odysseus SearXNG on the host.
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__Default=Host=postgres;Port=5432;Database=hydraforge;Username=hydraforge;Password=change-this-postgres-password

Jwt__Issuer=HydraForge
Jwt__Audience=HydraForge.Clients
Jwt__SigningKey=change-this-to-at-least-32-random-characters
Jwt__AccessTokenMinutes=60

AdminSeed__Username=admin
AdminSeed__Password=change-this-admin-password

Logging__MinimumLevel=Information
# Reuse Odysseus's SearXNG running on the host (127.0.0.1:8080).
# host.docker.internal resolves to the Docker host from inside a container.
# If running without Docker (dotnet run), change to http://localhost:8080.
# If running your own SearXNG via --profile search, change to http://searxng:8080.
SearXng__BaseUrl=http://host.docker.internal:8080
```

- [ ] **Step 3: Fill `docker-compose.yml`**

Use pgvector-ready PostgreSQL image so Task 3 migrations can create vector extension.

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    ports:
      # 5433 on host → 5432 in container. Avoids conflict with local postgres or
      # any other service using 5432 on the host (e.g. local dev postgres install).
      - "${POSTGRES_PORT:-5433}:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5

  server:
    build:
      context: .
      dockerfile: Dockerfile
    depends_on:
      postgres:
        condition: service_healthy
    env_file:
      - .env
    ports:
      # 5000 on host → 8080 in container. Host 8080 is taken by Odysseus SearXNG.
      - "5000:8080"
    extra_hosts:
      # Allows the container to reach services on the Docker host, including
      # Odysseus's SearXNG at http://host.docker.internal:8080.
      - "host.docker.internal:host-gateway"

  searxng:
    # Only needed if Odysseus (or another SearXNG instance) is NOT running.
    # Default: reuse Odysseus's SearXNG at http://host.docker.internal:8080.
    # To use this instead: docker compose --profile search up
    # and set SearXng__BaseUrl=http://searxng:8080 in .env
    image: searxng/searxng:latest
    profiles: ["search"]
    ports:
      - "8082:8080"

volumes:
  postgres-data:
```

- [ ] **Step 4: Verify Compose config parses**

Run:

```bash
cp .env.example .env
docker compose config
```

Expected: expanded Compose config prints without parse errors.

- [ ] **Step 5: Verify server image builds**

Run:

```bash
docker compose build server
```

Expected: server image builds through `dotnet publish`.

- [ ] **Step 6: Commit task branch**

```bash
git add Dockerfile docker-compose.yml .env.example
git commit -m "feat: add local docker stack"
git push
```
