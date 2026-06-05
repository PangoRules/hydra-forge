# Task 5: Attachment storage abstraction, local file store default, S3-compatible opt-in, attachment APIs
**Branch:** `task/attachments-file-storage`
**Parent branch:** `feat/phase-2-project-space-api-domain`
**Parent spec:** `2026-06-04-phase-2-project-space-api-domain-design.md`

**Goal:** Ship card attachment upload/list/download/delete backed by `IFileStore`, local filesystem default, S3-compatible opt-in config, and smoke tests.

**Files:** Modify/read `Attachment.cs`, `DomainErrorCodes.cs`, `HydraForge.Infrastructure.csproj`, `.env.example`, `appsettings.json`, `PersistenceServiceCollectionExtensions.cs`, `ProblemDetailsMapper.cs`, `Program.cs`. Create `src/HydraForge.Application/Attachments/*`, `src/HydraForge.Infrastructure/FileStorage/*`, `src/HydraForge.Infrastructure/Attachments/*`, `src/HydraForge.Server/Controllers/Projects/CardAttachmentsController.cs`, tests, smoke `http/phase-2/attachments.http`.

## Steps

- [ ] Decide package for S3-compatible implementation. Prefer `AWSSDK.S3` only in Infrastructure. Add package in `HydraForge.Infrastructure.csproj`.
- [ ] Write Application tests: rejects too-large file, unsupported content type, non-member, missing card; sanitizes display filename; generates opaque storage key; stores metadata only after file-store success; delete removes metadata then attempts file delete safely.
- [ ] Add error codes: `ATTACHMENT_NOT_FOUND`, `ATTACHMENT_UNSUPPORTED_CONTENT_TYPE`, `ATTACHMENT_FILE_TOO_LARGE`, `ATTACHMENT_FILE_STORE_UNAVAILABLE`.
- [ ] Define `IFileStore` in Application: `StoreAsync(Stream content, string contentType, CancellationToken)`, `OpenReadAsync(string storageKey, CancellationToken)`, `DeleteAsync(string storageKey, CancellationToken)` returning `Result`/`Result<T>`.
- [ ] Implement `AttachmentService` with size/content-type config options (`FileStorage:MaxBytes`, allowlist).
- [ ] Implement `LocalFileStore` with default path outside repo source tree, e.g. `/var/lib/hydraforge/attachments` in container or `AppContext.BaseDirectory/App_Data/attachments` for dev. Ensure generated key uses project/card/date/guid, not user filename.
- [ ] Implement `S3FileStore` opt-in with endpoint, bucket, region, access key, secret, force-path-style. Wrap SDK failures into safe errors.
- [ ] Update `.env.example` with `FileStorage__Provider=Local`, `FileStorage__LocalPath=/data/hydraforge/attachments`, commented S3 keys.
- [ ] Update `appsettings.json` with safe Local defaults.
- [ ] Wire DI based on `FileStorage:Provider`/`FILE_STORAGE_PROVIDER`.
- [ ] Write Server multipart tests for upload/list/download/delete; use fake `IFileStore` in tests.
- [ ] Implement controller: `POST/GET /attachments`, `GET/DELETE /attachments/{attachmentId}`. Download returns stored stream with metadata content type/name.
- [ ] Create `http/phase-2/attachments.http` with multipart upload, list, download, delete, bad type, too large note.
- [ ] Run attachment tests and `dotnet test`.
- [ ] Commit: `git add src tests http .env.example && git commit -m "feat: add card attachments"`.

**Acceptance:** local storage works without external service; S3 disabled unless selected; raw storage exceptions do not leak; `.http` covers every attachment endpoint.
