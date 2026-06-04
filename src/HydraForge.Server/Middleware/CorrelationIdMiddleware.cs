using System.Security.Cryptography;

namespace HydraForge.Server.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string CorrelationIdItemKey = "CorrelationId";
    private const string GeneratedPrefix = "req_";
    private const int GeneratedLength = 16;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
            && headerValue.ToString().Length <= 128)
        {
            return headerValue.ToString();
        }

        return GenerateCorrelationId();
    }

    private static string GenerateCorrelationId()
    {
        var bytes = new byte[GeneratedLength];
        RandomNumberGenerator.Fill(bytes);
        return GeneratedPrefix + Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}