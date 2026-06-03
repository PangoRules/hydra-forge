namespace HydraForge.Application.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, string Username, bool IsAdmin);