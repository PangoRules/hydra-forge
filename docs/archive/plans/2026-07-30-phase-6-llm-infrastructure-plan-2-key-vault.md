# Plan 2: IKeyVault + AesGcmKeyVault + startup validation + migration

**Branch:** `task/key-vault`
**Parent branch:** `feat/phase-6-llm-infrastructure`
**Parent spec:** `2026-07-30-phase-6-llm-infrastructure-design.md` — Task 2

## Steps

### 1. Define `IKeyVault` port in Application
- File: `src/HydraForge.Application/Llm/IKeyVault.cs`
- Interface: `string Encrypt(string plaintext); string Decrypt(string ciphertext);`

### 2. Implement `AesGcmKeyVault` in Infrastructure
- File: `src/HydraForge.Infrastructure/Llm/AesGcmKeyVault.cs`
- Constructor takes `IConfiguration` (or `IOptions<LlmOptions>`). Read `Llm:EncryptionKey` (base64 32-byte key).
- `Encrypt`: generate 12-byte nonce via `RandomNumberGenerator`, encrypt with `AesGcm`, return `v1:{nonceB64}:{ciphertextB64}:{tagB64}`.
- `Decrypt`: parse versioned prefix, extract nonce/ciphertext/tag, decrypt. Throw on tag mismatch.
- Register in DI: `services.AddSingleton<IKeyVault, AesGcmKeyVault>()` in a new `LlmServiceCollectionExtensions` or in `PersistenceServiceCollectionExtensions`.

### 3. Startup validation
- In `PersistenceServiceCollectionExtensions.AddPersistence` (or dedicated extension), validate `Llm:EncryptionKey`:
  - Decode base64, assert length == 32.
  - Missing/invalid → `InvalidOperationException("Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key.")`.
  - Fail-fast: server refuses to start.

### 4. Migration to re-encrypt placeholder rows
- Add migration that reads all `LlmProvider` rows where `ApiKeyEncrypted` is empty or matches known sentinel, encrypts via `IKeyVault`, and updates.
- Since migrations can't use DI, use a static helper that takes the raw key bytes. Migration class calls `AesGcmKeyVault.EncryptStatic(plaintext, keyBytes)`.
- Idempotent: skip already-encrypted rows (those starting with `v1:`).

### 5. Register DI
- File: `src/HydraForge.Infrastructure/Llm/LlmServiceCollectionExtensions.cs` (new)
- `services.AddLlmInfrastructure(configuration)` — registers `IKeyVault`, validates key, returns services.

## Verification
- `dotnet build` — all projects compile.
- `dotnet test` — existing tests pass.
- Unit test: `AesGcmKeyVault` round-trip encrypt/decrypt, tamper detection (modified ciphertext throws).
- `dotnet ef migrations has-pending-model-changes` — clean.