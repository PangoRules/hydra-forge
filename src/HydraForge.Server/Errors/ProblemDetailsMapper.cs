using System.Text.Json;
using HydraForge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Errors;

public static class ProblemDetailsMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ProblemDetails FromError(Error error, string correlationId)
    {
        var (status, title) = error.Code switch
        {
            DomainErrorCodes.Auth.InvalidCredentials => (401, "Invalid credentials"),
            DomainErrorCodes.Auth.UserDisabled => (403, "Access denied"),
            DomainErrorCodes.Auth.AdminSeedNotConfigured => (500, "Internal server error"),
            DomainErrorCodes.Infrastructure.DatabaseUnavailable => (503, "Service unavailable"),
            DomainErrorCodes.Infrastructure.AuditWriteFailed => (500, "Internal server error"),
            _ => (400, "Bad request"),
        };

        var type = $"https://hydraforge.local/errors/{ToKebabCase(error.Code)}";

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
        };

        details.Extensions["correlationId"] = correlationId;
        details.Extensions["code"] = error.Code;

        return details;
    }

    private static string ToKebabCase(string code)
    {
        if (string.IsNullOrEmpty(code)) return code;

        var result = new char[code.Length + (code.Count(char.IsUpper) - 1)];
        var j = 0;

        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (char.IsUpper(c) && i > 0)
            {
                result[j++] = '_';
                result[j++] = char.ToLower(c);
            }
            else
            {
                result[j++] = char.ToLower(c);
            }
        }

        return new string(result);
    }
}