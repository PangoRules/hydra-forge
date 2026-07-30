## Validate: Phase 6 Plan 2 — IKeyVault + AesGcmKeyVault + startup validation + migration

### Setup
- [ ] Postgres + MinIO up (`docker compose up -d postgres minio`)
- [ ] `Llm:EncryptionKey` set in shell env as base64-encoded 32-byte key (e.g. `export Llm__EncryptionKey="$(openssl rand -base64 32)"`)
- [ ] `ConnectionStrings__Default` set (or `appsettings.Development.json` pointing at port 5433)
- [ ] Server builds clean: `dotnet build`
- [ ] `dotnet test` — 715 pass

### Happy Path
1. `dotnet run --project src/HydraForge.Server` with valid `Llm:EncryptionKey` → server starts, no `InvalidOperationException`
2. Call `IKeyVault.Encrypt("plaintext")` → returns string starting with `v1:` containing 3 base64 segments (nonce, ciphertext, tag)
3. Call `IKeyVault.Decrypt(ciphertext)` with the same instance → returns the original plaintext
4. Two `Encrypt` calls on the same plaintext → produce different ciphertexts (random nonce) but both decrypt back to plaintext
5. Apply migration `20260731000000_ReencryptLlmProviderApiKeys` against a DB with `llm_providers` rows containing `api_key_encrypted = 'placeholder'` → rows update to `v1:...` ciphertext
6. Re-apply migration (down then up, or run on already-migrated DB) → `__EFMigrationsHistory` records the migration; no rows re-encrypted (idempotent — `v1:` rows skipped)

### Edge Cases
1. `Llm:EncryptionKey` missing → server startup throws `InvalidOperationException("Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key.")`
2. `Llm:EncryptionKey` whitespace only → same exception as above
3. `Llm:EncryptionKey` non-base64 (e.g. `not-base64!!!`) → same exception
4. `Llm:EncryptionKey` valid base64 but wrong length (16 bytes, 24 bytes) → same exception
5. Migration runs without `Llm__EncryptionKey` env var → throws `InvalidOperationException("'Llm:EncryptionKey' environment variable must be set to a base64-encoded 32-byte AES-256 key.")`
6. Tamper with ciphertext (flip a byte in the base64-decoded ciphertext segment) → `IKeyVault.Decrypt` throws (AuthenticationTagMismatch)
7. Decrypt with a different key than the encrypt key → `IKeyVault.Decrypt` throws
8. Decrypt `v0:abc:def:ghi` (unsupported version) → `IKeyVault.Decrypt` throws `InvalidOperationException("Unsupported ciphertext version.")`
9. Decrypt malformed ciphertext (`v1:only-one-segment`) → `IKeyVault.Decrypt` throws `InvalidOperationException("Invalid ciphertext format.")`
10. DB row with `api_key_encrypted = ''` (empty) → migration skips (no encryption applied)
11. DB row with `api_key_encrypted` already `v1:...` → migration skips (idempotent)

### Regressions
1. `dotnet ef migrations has-pending-model-changes` → clean
2. Server `GET /health` returns 200 with `LlmProvider` health probe listed
3. All 14 `WebApplicationFactory` test fixtures boot with the hard-coded test key (`0YEf4ZBA47CpqWSH0ZczKZ62owvbQ7T5IRfcecZ4Vgo=`)
4. `AesGcmKeyVaultTests` (8 tests) pass: round-trip, tamper, wrong key, format, invalid base64, wrong length, unsupported version, static/instance parity
5. `./tests/HydraForge.Server.Tests` `dotnet test` still passes 168 tests with `Llm:EncryptionKey` set in fixtures
6. `PersistenceServiceCollectionExtensions.AddPersistence` still wires audit + health probes + LLM infrastructure (`AddLlmInfrastructure`)

### Cleanup
- [ ] Unset `Llm:EncryptionKey` after manual test
- [ ] Revert any test rows inserted into `llm_providers` (or drop and recreate via `dotnet ef database update` from a clean baseline)
