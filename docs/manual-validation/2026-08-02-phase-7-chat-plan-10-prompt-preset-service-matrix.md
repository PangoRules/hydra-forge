## Validate: Prompt Preset Service

### Setup
- [ ] Start API with Phase 7 chat migrations applied and authenticate as two distinct regular users.

### Happy Path
1. Create an ungrouped preset with non-empty name and content → preset is returned for owner with `GroupId = null`.
2. Create a preset group, then create a preset in it → group listing includes preset under that group.
3. Update preset name, content, and group → returned preset contains all changes and a newer `UpdatedAt`.
4. Rename group → returned group contains new name and a newer `UpdatedAt`.
5. Archive preset → preset receives `ArchivedAt` and no longer appears in active preset listings.
6. Archive group containing presets → group receives `ArchivedAt`; presets remain active and become ungrouped.

### Edge Cases
1. Create or update preset with blank name → validation error with `VALIDATION_REQUIRED` code.
2. Create or update group with blank name → validation error with `VALIDATION_REQUIRED` code.
3. Assign preset to missing group → `CHAT_PRESET_GROUP_NOT_FOUND` error; preset remains unchanged.
4. User B assigns preset to User A's group → `CHAT_PRESET_GROUP_NOT_OWNER` error; preset remains unchanged.
5. User B updates or archives User A's preset → `CHAT_PRESET_NOT_OWNER` error; preset remains unchanged.
6. User B updates or archives User A's group → `CHAT_PRESET_GROUP_NOT_OWNER` error; group remains unchanged.
7. List presets without group filter → all owner's active presets are returned.
8. List presets with empty group filter → only owner's active ungrouped presets are returned.
9. List presets with group GUID → only owner's active presets in that group are returned.

### Regressions
1. Archive group containing multiple presets → no preset is deleted or archived.
2. Rename group after presets exist → presets remain attached and appear in returned group.
3. Update preset without group → preset becomes ungrouped without changing ownership.
4. List groups after one group is archived → archived group is excluded; active groups remain intact.

### Cleanup
- [ ] Archive created presets and groups; remove test users/data according to local test-environment procedure.
