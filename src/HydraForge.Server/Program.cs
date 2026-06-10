using System.Text;
using System.Text.Json.Serialization;
using HydraForge.Application.Auth;
using HydraForge.Application.Health;
using HydraForge.Infrastructure.Attachments;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Cards;
using HydraForge.Infrastructure.Checklist;
using HydraForge.Infrastructure.Columns;
using HydraForge.Infrastructure.Comments;
using HydraForge.Infrastructure.Persistence;
using HydraForge.Infrastructure.Projects;
using HydraForge.Server.Auth;
using HydraForge.Server.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddProjectServices();
builder.Services.AddColumnServices();
builder.Services.AddCardServices();
builder.Services.AddChecklistServices();
builder.Services.AddCommentServices();
builder.Services.AddAttachmentServices(builder.Configuration);

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.UserIdRequired, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => context.User.TryGetUserId(out _));
    });
});

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
builder.Services.AddScoped<TestUserSeeder>();
builder.Services.AddScoped<GetHealthHandler>(sp =>
    new GetHealthHandler(sp.GetServices<IHealthProbe>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

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

    if (app.Environment.IsDevelopment())
    {
        var testUserSeeder = scope.ServiceProvider.GetRequiredService<TestUserSeeder>();
        await testUserSeeder.SeedIfNeededAsync();
    }
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
