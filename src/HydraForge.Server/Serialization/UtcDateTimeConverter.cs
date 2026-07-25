using System.Text.Json;
using System.Text.Json.Serialization;

namespace HydraForge.Server.Serialization;

// System.Text.Json's default DateTime parsing converts any string with an
// explicit non-'Z' offset (e.g. "+00:00", which .NET's own DateTimeOffset
// serializer emits even for UTC) to the server's local time zone and marks it
// Kind=Local. Npgsql rejects Kind=Local for "timestamp with time zone"
// columns, so any client round-tripping a date-time field through a
// DateTimeOffset type (NSwag's default for OpenAPI "format: date-time")
// 500s on save. This normalizes every incoming/outgoing DateTime to UTC
// regardless of the offset notation used on the wire.
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    // Only Local is corrected — Utc passes through untouched, and Unspecified
    // (no offset on the wire) is left exactly as System.Text.Json already
    // parses it, since Npgsql's "timestamp with time zone" mapping treats
    // Unspecified as already-UTC. Widening this to touch Unspecified too would
    // silently shift naive date-time payloads by the server's local offset.
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }

    // No Kind coercion here — Utf8JsonWriter.WriteStringValue(DateTime) already
    // produces the same ISO-8601 output System.Text.Json's default converter
    // would, so this is a passthrough that changes nothing for existing callers.
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
