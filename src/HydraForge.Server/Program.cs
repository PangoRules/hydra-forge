using System.Text;
using HydraForge.Application.Auth;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

