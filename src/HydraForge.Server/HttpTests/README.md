# HTTP Smoke Tests

Self-contained REST smoke tests for HydraForge Phase 2 APIs. Each file authenticates, sets up its own project/data, exercises endpoints, and cleans up.

## Run Order

1. **Run `Auth.http` first** — extracts `{{accessToken}}` and `{{userId}}` for all other files.
2. Run other `.http` files in any order — each is fully independent.
3. **Tip:** Run `Auth.http` once, then use the returned token as a variable in other files via `{{loginAdmin.response.body.$.accessToken}}`.

## File Index

| File | Endpoints Covered |
|------|-------------------|
| `Auth.http` | `POST /api/auth/login` |
| `Projects.http` | `GET/POST /api/projects`, `GET/PUT/DELETE /api/projects/{id}`, `GET/POST/PUT/DELETE /api/projects/{id}/members` |
| `Columns.http` | `GET/POST /api/projects/{id}/columns`, `GET/PUT/DELETE /api/projects/{id}/columns/{colId}`, `PUT /api/projects/{id}/columns/reorder` |
| `Cards.http` | `GET/POST /api/projects/{id}/cards`, `GET/PUT/DELETE /api/projects/{id}/cards/{cardId}`, `POST /api/projects/{id}/cards/{cardId}/move`, `POST/DELETE /api/projects/{id}/cards/{cardId}/assignees`, `POST /api/projects/{id}/cards/{cardId}/archive` |
| `CardChecklist.http` | `GET/POST /api/projects/{id}/cards/{cardId}/cardchecklist`, `PUT /api/projects/{id}/cards/{cardId}/cardchecklist/{itemId}`, `PATCH /api/projects/{id}/cards/{cardId}/cardchecklist/{itemId}/toggle`, `PUT .../cardchecklist/{itemId}/reorder`, `DELETE /api/projects/{id}/cards/{cardId}/cardchecklist/{itemId}` |
| `CardComments.http` | `GET/POST /api/projects/{id}/cards/{cardId}/cardcomments`, `PUT /api/projects/{id}/cards/{cardId}/cardcomments/{commentId}`, `DELETE /api/projects/{id}/cards/{cardId}/cardcomments/{commentId}` |
| `CardAttachments.http` | `GET/POST /api/projects/{id}/cards/{cardId}/attachments`, `GET/DELETE /api/projects/{id}/cards/{cardId}/attachments/{attachmentId}` |
| `Specs.http` | `POST/GET /api/projects/{id}/specs/cards/{cardId}`, `GET/PUT /api/projects/{id}/specs/{specId}`, `GET /api/projects/{id}/specs/{specId}/versions`, `POST /api/projects/{id}/specs/{specId}/restore` |
| `Plans.http` | `POST/GET /api/projects/{id}/plans/cards/{cardId}`, `GET/PUT /api/projects/{id}/plans/{planId}`, `GET /api/projects/{id}/plans/{planId}/versions`, `POST /api/projects/{id}/plans/{planId}/restore` |
| `CardRelationships.http` | `GET/POST /api/projects/{id}/cards/{cardId}/cardrelationships`, `DELETE /api/projects/{id}/cards/{cardId}/cardrelationships/{relId}`, `GET /api/projects/{id}/cards/{cardId}/cardrelationships/archive-impact`, `POST /api/projects/{id}/cards/{cardId}/cardrelationships/archive-with-relationships` |
| `ProjectSnapshot.http` | `GET /api/projects/{id}/ProjectSnapshot` |
| `Realtime.http` | `POST /hubs/board/negotiate`, `POST /hubs/presence/negotiate` |
| `Health.http` | `GET /health` |

## Phase 2 Endpoint Coverage

All endpoints from `docs/superpowers/specs/2026-06-04-phase-2-project-space-api-domain-design.md` (lines 87–104) are covered:

- Project CRUD + membership + archive ✅
- Column CRUD + reorder ✅
- Card CRUD + move + assignees + archive ✅
- Checklist item CRUD + toggle + reorder ✅
- Comment CRUD + archive ✅
- Attachment upload/list/download/delete ✅
- Spec/Plan CRUD + version list + restore ✅
- Relationship create/delete + archive-impact preflight ✅
- ProjectContextSnapshot ✅

## Environment

Tests target `http://localhost:5000` (default `dotnet run` host). Override with `@baseUrl` variable:

```
@baseUrl = http://localhost:5000
```

## Seeded Users

| Username | Password | Role |
|----------|----------|------|
| `testadmin` | `TestAdmin123!` | Admin |
| `nonmember` | `NonMember123!` | Non-project member (403 tests) |

## Prerequisites

- Server must be running (`dotnet run --project src/HydraForge.Server`)
- Database must be seeded (admin user + non-member user exist)
- MinIO optional for attachment upload tests (server falls back to local file store if MinIO unavailable)

## Running

In VS Code with REST Client extension, click `Send Request` above each block. Or use `httpi` / `rest-client` CLI.

## Variables

Each file uses these variable patterns:

- `{{adminToken}}` — bearer token from `Auth.http` login
- `{{loginAdmin.response.body.$.accessToken}}` — inline token extraction from prior response
- `{{$guid}}` — unique correlation ID per request (REST Client built-in)
- `{{baseUrl}}` — host URL, default `http://localhost:5000`
