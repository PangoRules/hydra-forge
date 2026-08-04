namespace HydraForge.Server.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Only over HTTPS — sending this over plain HTTP tells the browser to
            // refuse future plain-HTTP connections to this host, which would lock
            // out any deployment that hasn't done the TLS setup yet.
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
