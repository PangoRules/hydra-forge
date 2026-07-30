using System.Text.Json;

namespace HydraForge.Application.Audit;

/// <summary>
/// Serializes before/after entity snapshots for <see cref="AuditLogRequest"/>.
/// Callers pass small, flat records built from in-memory entity state —
/// never the full EF entity (nav properties would cycle) or a response DTO
/// that requires extra queries the audit trail doesn't need.
/// </summary>
public static class AuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? Serialize<T>(T? value) =>
        value is null ? null : JsonSerializer.Serialize(value, Options);
}
