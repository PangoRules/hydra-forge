using System.Text;
using HydraForge.Application.Auth;
using HydraForge.Application.Health;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Persistence;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddPersistence(builder.Configuration);

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HydraForge";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HydraForge";
var jwtSigningKey =
    builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required");
var accessTokenMinutes = builder.Configuration.GetValue<int>("Jwt:AccessTokenMinutes", 60);

builder.Services.Configure<Argon2Options>(builder.Configuration.GetSection("Argon2"));
builder.Services.Configure<AdminSeederOptions>(builder.Configuration.GetSection("AdminSeed"));

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
builder.Services.AddSingleton<IAccessTokenIssuer>(sp => new JwtTokenIssuer(
    jwtIssuer,
    jwtAudience,
    jwtSigningKey,
    accessTokenMinutes
));
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<GetHealthHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health check endpoint
app.MapGet("/health", async (GetHealthHandler handler, CancellationToken ct) =>
{
    var result = await handler.HandleAsync(ct);

    if (!result.IsSuccess)
    {
        return Results.Problem(
            statusCode: 503,
            title: "Service unavailable",
            detail: "Health check failed"
        );
    }

    var response = result.Value;
    var httpStatus = response.OverallStatus == HealthStatus.Unhealthy
        ? StatusCodes.Status503ServiceUnavailable
        : StatusCodes.Status200OK;

    return Results.Json(new
    {
        status = response.OverallStatus.ToString().ToLowerInvariant(),
        components = new[]
        {
            new { name = "server", status = response.ServerStatus.ToString().ToLowerInvariant(), detail = "Server is running." },
            new { name = "database", status = response.DatabaseStatus.ToString().ToLowerInvariant(), detail = response.DatabaseStatus == HealthStatus.Healthy ? "Database is connected." : "Database issue detected." },
            new { name = "llmProviders", status = response.LlmStatus.ToString().ToLowerInvariant(), detail = response.LlmStatus == HealthStatus.NotConfigured ? "No LLM providers configured." : "LLM providers available." }
        }
    }, statusCode: httpStatus);
});

var applyMigrationsOnStartup = app.Configuration.GetValue<bool>(
    "Database:ApplyMigrationsOnStartup",
    true
);
if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HydraForgeDbContext>();
    db.Database.Migrate();

    var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await adminSeeder.SeedIfNeededAsync();
}

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("Endpoint", httpContext.Request.Path);
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"] as string ?? "unknown");
    };
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }

