## Validate: Phase 7 Chat Plan 13 — Chat Search Service

### Setup
- [ ] `dotnet run --project src/HydraForge.Server` (Postgres + MinIO on `docker compose up -d postgres minio`)
- [ ] Authenticated session as `userA` and a separate session as `userB`
- [ ] `userA` owns at least 3 chat sessions: one with title containing the unique title token `findme`, one with title NOT containing it but with a message containing the unique content token `bodyfind`, and one in a different `ProjectId` with title containing `findme`
- [ ] `userA` also owns one **archived** session whose title contains `findme` (archive via `DELETE /api/chat/sessions/{id}` before search)
- [ ] `userB` owns one session whose title AND message both contain `findme` (cross-tenant isolation check)

### Happy Path
1. `IChatSearchService.SearchAsync(userA.Id, "findme")` → returns the active `userA` session whose title matches; `MatchedOn = "Title"`, `Snippet = null`
2. `IChatSearchService.SearchAsync(userA.Id, "bodyfind")` → returns the `userA` session whose message matches; `MatchedOn = "Content"`, `Snippet` ~200 chars centered on the hit and contains `bodyfind`
3. `IChatSearchService.SearchAsync(userA.Id, "findme", projectId: <otherProjectId>)` → returns ONLY the session scoped to that `ProjectId`; ignores other `userA` sessions
4. Same session has both title hit AND content hit → appears ONCE in results with `MatchedOn = "Title"` (title wins, content merge suppressed by `seen` HashSet)
5. Search is case-insensitive: query `FINDME` matches `findme` titles (ILIKE on Postgres side, `OrdinalIgnoreCase` in `BuildSnippet`)

### Edge Cases
1. Query is `""` or whitespace → repo layer short-circuits to empty list; service returns `[]` (no N+1, no empty-string LIKE pattern)
2. `BuildSnippet`: query token at index 0 of message → result starts with the token (no leading `…`)
3. `BuildSnippet`: query token near end → result ends with `…` and shifts start back so total window stays ~200 chars
4. `BuildSnippet`: content shorter than `SnippetLength` (200) → returns whole content, no `…`
5. Content match but `GetByIdAsync` returns null for that session (e.g. session deleted between two repos) → result still appears with `SessionTitle = ""` (empty string fallback, not null)
6. 30 sessions match title, 30 match content, all unique → results capped at 20 (one per session, deduped via `seen`); order matches `titleResults` then `contentResults`

### Regressions
1. `IChatSessionRepository` / `IChatMessageRepository` interfaces gained `SearchByTitleAsync` / `SearchByContentAsync` — every existing fake in `tests/HydraForge.Application.Tests/Chat/` (`ChatSessionServiceTests`, `ChatMessageServiceTests`, `ChatFolderServiceTests`) still compiles and its existing tests still pass
2. Archived chat session in `userA` does NOT appear in search results (mirror of the round-2 fix: `ArchivedAt == null` filter present in both `EfChatSessionRepository.SearchByTitleAsync` and `EfChatMessageRepository.SearchByContentAsync` sessionIds subquery)
3. `userB`'s session never appears in `userA`'s search results (owner-scoped via `OwnerId == userId`)
4. Other chat services unchanged: `ChatSessionService`, `ChatMessageService`, `ChatFolderService`, `CardChatLinkService`, `PromptPresetService` still pass their test suites

### Cleanup
- [ ] Delete the test sessions and the cross-tenant `userB` session (or leave for next iteration — none hold destructive resources)
