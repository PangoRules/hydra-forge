using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HydraForge.Application.Admin;
using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Health;
using HydraForge.Domain.Constants;
using HydraForge.Infrastructure.Attachments;
using HydraForge.Infrastructure.Audit;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Cards;
using HydraForge.Infrastructure.Checklist;
using HydraForge.Infrastructure.Columns;
using HydraForge.Infrastructure.Comments;
using HydraForge.Infrastructure.Notifications;
using HydraForge.Infrastructure.Persistence;
using HydraForge.Infrastructure.Plans;
using HydraForge.Infrastructure.Projects;
using HydraForge.Infrastructure.Realtime;
using HydraForge.Infrastructure.Settings;
using HydraForge.Infrastructure.Specs;
using HydraForge.Server.Auth;
using HydraForge.Server.Hubs;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            )
);

var corsOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:3000";
var corsOriginList = corsOrigins.Split(
    ',',
    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOriginList).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    );
});

// AddFixedWindowLimiter(name, configure) creates one counter shared by every caller of
// that policy — not per-client, despite "per IP" in the comments below. A single caller
// sending 5 rapid requests locked out every other user for the rest of the window. Using
// AddPolicy + RateLimitPartition instead gives each client IP its own independent counter.
// Reads Connection.RemoteIpAddress directly, so it sees the reverse-proxy IP once this app
// sits behind one — becomes proxy-aware when forwarded-headers middleware is added later.
static string ClientIp(HttpContext httpContext) =>
    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Strict login limit: 5 attempts per minute per IP
    options.AddPolicy(
        "Login",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                }
            )
    );

    // Global limit: 300 requests per minute per IP
    options.AddPolicy(
        "Global",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10,
                }
            )
    );

    // SignalR hubs: 60 messages per minute per connection's underlying IP
    options.AddPolicy(
        "SignalR",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5,
                }
            )
    );
});

builder.Services.AddOpenApi(options =>
    options.AddSchemaTransformer<HydraForge.Server.OpenApi.EnumSchemaTransformer>()
);
builder
    .Services.AddControllers(options =>
    {
        options.Conventions.Add(new HydraForge.Server.Conventions.RateLimitConvention("Global"));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(
            new HydraForge.Server.Serialization.UtcDateTimeConverter()
        );
    });
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddProjectServices();
builder.Services.AddColumnServices();
builder.Services.AddCardServices();
builder.Services.AddChecklistServices();
builder.Services.AddCommentServices();
builder.Services.AddAttachmentServices(builder.Configuration);
builder.Services.AddSpecServices();
builder.Services.AddPlanServices();
builder.Services.AddNotificationServices();
builder.Services.AddSettingsServices();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HydraForge";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HydraForge";
var jwtSigningKey =
    builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is required");

if (jwtSigningKey == "your-256-bit-secret-key-here-replace-in-production")
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be changed from the default placeholder. "
            + "Set it via environment variable Jwt__SigningKey or user-secrets."
    );
}

var accessTokenMinutes = builder.Configuration.GetValue("Jwt:AccessTokenMinutes", 60);

builder.Services.Configure<Argon2Options>(builder.Configuration.GetSection("Argon2"));

// Validate Argon2 parameters at startup
Argon2Options argon2Options = builder.Configuration.GetSection("Argon2").Get<Argon2Options>()!;
if (argon2Options.Iterations < 2)
    throw new InvalidOperationException("Argon2:Iterations must be at least 2.");
if (argon2Options.MemorySizeKiB < 32768)
    throw new InvalidOperationException("Argon2:MemorySizeKiB must be at least 32768 (32 MiB).");
if (argon2Options.Parallelism < 1)
    throw new InvalidOperationException("Argon2:Parallelism must be at least 1.");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust the reverse proxy — in production, restrict to known proxy IPs
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (
                    !string.IsNullOrEmpty(accessToken)
                    && (
                        path.StartsWithSegments("/hubs/board")
                        || path.StartsWithSegments("/hubs/presence")
                        || path.StartsWithSegments("/hubs/notifications")
                    )
                )
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        AuthPolicies.UserIdRequired,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.TryGetUserId(out _));
        }
    )
    .AddPolicy(AuthPolicies.AdminRequired, policy => policy.RequireRole(Roles.Admin));

builder
    .Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        // Without this, hub payload enums (BoardEntityType, BoardAction, etc.) serialize
        // as ints — MVC's JsonStringEnumConverter (above) only covers REST responses, not
        // the SignalR hub protocol. The TUI client deserializes these fields as strings,
        // so a mismatched int throws inside the client's message handler and is silently
        // swallowed by the SignalR client, making board-event pushes a silent no-op.
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAuditLogReader, EfAuditLogReader>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
builder.Services.AddSingleton<IAccessTokenIssuer>(sp => new JwtTokenIssuer(
    jwtIssuer,
    jwtAudience,
    jwtSigningKey,
    accessTokenMinutes
));
builder.Services.AddScoped<LoginUserHandler>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<TestUserSeeder>();
builder.Services.AddScoped(sp => new GetHealthHandler(sp.GetServices<IHealthProbe>()));

builder.Services.AddRealtimeServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var applyMigrationsOnStartup = app.Configuration.GetValue(
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

    if (app.Environment.IsDevelopment())
    {
        var testUserSeeder = scope.ServiceProvider.GetRequiredService<TestUserSeeder>();
        await testUserSeeder.SeedIfNeededAsync();
    }
}

// Initialize file store (bucket creation for S3/MinIO, no-op for Local)
var fileStore = app.Services.GetRequiredService<HydraForge.Application.Attachments.IFileStore>();
var initResult = await fileStore.InitializeAsync();
if (initResult.IsFailure)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("File store initialization failed: {Error}", initResult.Error.Message);
}

// Must run before anything that reads the connection's remote IP — in particular
// app.UseRateLimiter() below, which partitions by httpContext.Connection.RemoteIpAddress.
// Placed ahead of request logging and CORS too, since ASP.NET Core guidance is for
// forwarded-headers to run first in the pipeline.
app.UseForwardedHeaders();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("Endpoint", httpContext.Request.Path);
        diagnosticContext.Set(
            "CorrelationId",
            httpContext.Items["CorrelationId"] as string ?? "unknown"
        );
    };
});

app.UseCors();

app.UseRateLimiter();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<BoardHub>("/hubs/board");
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
