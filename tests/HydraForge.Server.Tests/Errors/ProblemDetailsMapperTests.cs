using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HydraForge.Server.Tests.Errors;

public class ProblemDetailsMapperTests
{
    [Fact]
    public void FromError_AuthInvalidCredentials_MapsTo401()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-1");

        Assert.Equal(401, details.Status);
        Assert.Equal("Invalid credentials", details.Title);
        Assert.Equal("corr-1", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuthUserDisabled_MapsTo403()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.UserDisabled,
            "User account is disabled.");

        var details = ProblemDetailsMapper.FromError(error, "corr-2");

        Assert.Equal(403, details.Status);
        Assert.Equal("Access denied", details.Title);
        Assert.Equal("corr-2", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.UserDisabled, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuthAdminSeedNotConfigured_MapsTo500()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.AdminSeedNotConfigured,
            "Admin seed is not configured.");

        var details = ProblemDetailsMapper.FromError(error, "corr-3");

        Assert.Equal(500, details.Status);
        Assert.Equal("Internal server error", details.Title);
        Assert.Equal("corr-3", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.AdminSeedNotConfigured, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_DatabaseUnavailable_MapsTo503()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.DatabaseUnavailable,
            "Database is unavailable.");

        var details = ProblemDetailsMapper.FromError(error, "corr-4");

        Assert.Equal(503, details.Status);
        Assert.Equal("Service unavailable", details.Title);
        Assert.Equal("corr-4", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.DatabaseUnavailable, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuditWriteFailed_MapsTo500()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.AuditWriteFailed,
            "Failed to write audit entry.");

        var details = ProblemDetailsMapper.FromError(error, "corr-5");

        Assert.Equal(500, details.Status);
        Assert.Equal("Internal server error", details.Title);
        Assert.Equal("corr-5", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.AuditWriteFailed, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_UnknownCode_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error("UNKNOWN_CODE", "Some unknown error.");

        var details = ProblemDetailsMapper.FromError(error, "corr-6");

        Assert.Equal(400, details.Status);
        Assert.Equal("Bad request", details.Title);
        Assert.Equal("corr-6", details.Extensions["correlationId"]);
        Assert.Equal("UNKNOWN_CODE", details.Extensions["code"]);
    }

    [Fact]
    public void FromError_SetsTypeToHydraForgeErrorsUri()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-7");

        Assert.StartsWith("https://hydraforge.local/errors/", details.Type);
    }

    [Fact]
    public void FromError_ConvertsErrorCodeToReadableSlug()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-8");

        Assert.Equal("https://hydraforge.local/errors/auth-invalid-credentials", details.Type);
    }
}
