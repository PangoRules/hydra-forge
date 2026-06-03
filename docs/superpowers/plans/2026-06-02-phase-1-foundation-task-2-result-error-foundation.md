# Task 2: Domain result/error foundation
**Branch:** `task/phase-1-result-errors`
**Parent branch:** `feat/phase-1-foundation`
**Parent spec:** `2026-06-02-phase-1-foundation-design.md`

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development, then superpowers:executing-plans.

**Goal:** Add Domain-layer `Result<T>`, `Result`, `Error`, and named error codes for expected failures.

**Architecture:** Domain owns pure error primitives. Application and Server consume them later. No HTTP, EF Core, logging, or infrastructure imports in Domain.

**Tech Stack:** C# net10.0, xUnit, plain `Assert.*`.

---

## Files

- Create: `src/HydraForge.Domain/Common/Error.cs`
- Create: `src/HydraForge.Domain/Common/Result.cs`
- Create: `src/HydraForge.Domain/Common/DomainErrorCodes.cs`
- Modify: `tests/HydraForge.Domain.Tests/UnitTest1.cs` or replace with focused tests
- Read-only context: `src/HydraForge.Domain/HydraForge.Domain.csproj`, `tests/HydraForge.Domain.Tests/HydraForge.Domain.Tests.csproj`

## Steps

- [x] **Step 1: Replace placeholder Domain test with failing result tests**

Create `tests/HydraForge.Domain.Tests/Common/ResultTests.cs` and remove `UnitTest1.cs` after tests pass.

Cover:

```csharp
Error error = new(DomainErrorCodes.Auth.InvalidCredentials, "Invalid username or password.");
Result<string> failure = Result<string>.Failure(error);

Assert.False(failure.IsSuccess);
Assert.True(failure.IsFailure);
Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, failure.Error.Code);
Assert.Throws<InvalidOperationException>(() => failure.Value);
```

Also cover success value, `Result.Success()`, `Result.Failure(error)`, and preventing `Error.Code` from blank value.

- [x] **Step 2: Run failing Domain tests**

Expected: compile fails because `Error`, `Result`, and `DomainErrorCodes` do not exist.

- [x] **Step 3: Implement `Error` value object**

Use immutable sealed class with constructor validation.

```csharp
namespace HydraForge.Domain.Common;

public sealed class Error
{
    public Error(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Error code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Error message is required.", nameof(message));
        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}
```

- [x] **Step 4: Implement `Result<T>` and non-generic `Result`**

Behavior: success has value, failure has error, accessing wrong side throws `InvalidOperationException`.

- [x] **Step 5: Add named error code constants**

Create nested static classes in `DomainErrorCodes`:

```csharp
public static class DomainErrorCodes
{
    public static class Auth
    {
        public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string UserDisabled = "AUTH_USER_DISABLED";
        public const string AdminSeedNotConfigured = "AUTH_ADMIN_SEED_NOT_CONFIGURED";
    }

    public static class Infrastructure
    {
        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string AuditWriteFailed = "AUDIT_WRITE_FAILED";
        public const string LlmProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE";
    }
}
```

- [x] **Step 6: Run and pass Domain tests**

Expected: all Domain tests pass.

- [x] **Step 7: Run solution build**

Expected: build succeeds.

- [x] **Step 8: Commit task branch**

PR: https://github.com/PangoRules/hydra-forge/pull/2**
