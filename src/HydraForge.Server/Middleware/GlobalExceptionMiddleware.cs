using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, GetOptions());
        }
    }

    private static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        JsonSerializerOptions options
    )
    {
        var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;

        logger.LogError(
            exception,
            "Unhandled exception. CorrelationId: {CorrelationId}",
            correlationId
        );

        var problemDetails = new ProblemDetails
        {
            Status = 500,
            Title = "Internal server error",
            Type = "https://hydraforge.local/errors/internal-server-error",
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = 500;

        var json = JsonSerializer.Serialize(problemDetails, options);

        await context.Response.WriteAsync(json);
    }
}
