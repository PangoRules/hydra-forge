using System.Text;
using HydraForge.Application.Auth;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var applyMigrationsOnStartup = app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup", true)
    && !app.Environment.IsEnvironment("Testing")
    && app.Configuration.GetValue<bool>("Database:SkipMigrations", false) == false;
if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HydraForgeDbContext>();
    db.Database.Migrate();

    var adminSeeder = scope.ServiceProvider.GetRequiredService<AdminSeeder>();
    await adminSeeder.SeedIfNeededAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithTags("Test");

app.MapGet("/throw", () =>
{
    throw new InvalidOperationException("Test exception");
})
.WithName("Throw")
.WithTags("Test");

app.Run();

public partial class Program { }

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

