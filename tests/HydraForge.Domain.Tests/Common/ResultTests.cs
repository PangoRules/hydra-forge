using HydraForge.Domain.Common;

namespace HydraForge.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Error_Construction_WithCodeAndMessage_SetsProperties()
    {
        var error = new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid username or password.");

        Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, error.Code);
        Assert.Equal("Invalid username or password.", error.Message);
    }

    [Fact]
    public void Error_BlankCode_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Error("   ", "Some message"));
        Assert.Contains("Error code is required.", exception.Message);
    }

    [Fact]
    public void Error_BlankMessage_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Error("ERR_CODE", "   "));
        Assert.Contains("Error message is required.", exception.Message);
    }

    [Fact]
    public void Result_Failure_HasErrorAndNoValue()
    {
        var error = new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid username or password.");
        Result<string> failure = Result<string>.Failure(error);

        Assert.False(failure.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.Equal(DomainErrorCodes.Auth.InvalidCredentials, failure.Error.Code);
        Assert.Equal("Invalid username or password.", failure.Error.Message);
    }

    [Fact]
    public void Result_Failure_Value_ThrowsInvalidOperationException()
    {
        var error = new Error(DomainErrorCodes.Auth.InvalidCredentials, "Invalid username or password.");
        Result<string> failure = Result<string>.Failure(error);

        Assert.Throws<InvalidOperationException>(() => failure.Value);
    }

    [Fact]
    public void Result_Success_HasValueAndNoError()
    {
        Result<string> success = Result<string>.Success("test-value");

        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
        Assert.Equal("test-value", success.Value);
    }

    [Fact]
    public void Result_Success_Error_ThrowsInvalidOperationException()
    {
        Result<string> success = Result<string>.Success("test-value");

        Assert.Throws<InvalidOperationException>(() => _ = success.Error);
    }

    [Fact]
    public void Result_NonGeneric_Failure_HasErrorAndNoValue()
    {
        var error = new Error(DomainErrorCodes.Infrastructure.DatabaseUnavailable, "Database unavailable.");
        Result failure = Result.Failure(error);

        Assert.False(failure.IsSuccess);
        Assert.True(failure.IsFailure);
        Assert.Equal(DomainErrorCodes.Infrastructure.DatabaseUnavailable, failure.Error.Code);
    }

    [Fact]
    public void Result_NonGeneric_Success_HasNoError()
    {
        Result success = Result.Success();

        Assert.True(success.IsSuccess);
        Assert.False(success.IsFailure);
    }
}