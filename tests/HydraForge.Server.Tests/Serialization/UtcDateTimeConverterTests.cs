using System.Text.Json;
using HydraForge.Server.Serialization;

namespace HydraForge.Server.Tests.Serialization;

public class UtcDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new UtcDateTimeConverter() },
    };

    private record Payload(DateTime Value);

    [Fact]
    public void Read_ZOffset_StaysUtc()
    {
        var payload = JsonSerializer.Deserialize<Payload>(
            """{"Value":"2028-10-26T00:00:00Z"}""",
            Options
        );

        Assert.Equal(DateTimeKind.Utc, payload!.Value.Kind);
        Assert.Equal(new DateTime(2028, 10, 26, 0, 0, 0, DateTimeKind.Utc), payload.Value);
    }

    [Fact]
    public void Read_ExplicitNonZeroOffset_IsConvertedToUtcNotLocal()
    {
        // This is exactly what .NET's own DateTimeOffset JSON serializer emits for a
        // UTC instant — "+00:00" rather than "Z" — which is the shape that previously
        // reached Npgsql as Kind=Local and blew up with:
        // "Cannot write DateTime with Kind=Local to PostgreSQL type 'timestamp with time zone'".
        var payload = JsonSerializer.Deserialize<Payload>(
            """{"Value":"2028-10-26T00:00:00+00:00"}""",
            Options
        );

        Assert.Equal(DateTimeKind.Utc, payload!.Value.Kind);
        Assert.Equal(new DateTime(2028, 10, 26, 0, 0, 0, DateTimeKind.Utc), payload.Value);
    }

    [Fact]
    public void Read_NonUtcOffset_ConvertsClockTimeToUtcEquivalent()
    {
        // "-05:00" parses to Kind=Local under the default STJ DateTime converter;
        // the fix must shift the clock time to its correct UTC equivalent, not just
        // relabel the Kind on the original local-looking value.
        var payload = JsonSerializer.Deserialize<Payload>(
            """{"Value":"2028-10-26T00:00:00-05:00"}""",
            Options
        );

        Assert.Equal(DateTimeKind.Utc, payload!.Value.Kind);
        Assert.Equal(new DateTime(2028, 10, 26, 5, 0, 0, DateTimeKind.Utc), payload.Value);
    }

    [Fact]
    public void Read_NoOffset_StaysUnspecified()
    {
        // Naive date-times (no offset on the wire) must be left alone — Npgsql
        // treats Kind=Unspecified as already-UTC for "timestamp with time zone",
        // so silently shifting these by the server's local offset would corrupt them.
        var payload = JsonSerializer.Deserialize<Payload>(
            """{"Value":"2028-10-26T00:00:00"}""",
            Options
        );

        Assert.Equal(DateTimeKind.Unspecified, payload!.Value.Kind);
        Assert.Equal(new DateTime(2028, 10, 26, 0, 0, 0, DateTimeKind.Unspecified), payload.Value);
    }

    [Fact]
    public void Write_Utc_RoundTripsWithZSuffix()
    {
        var json = JsonSerializer.Serialize(
            new Payload(new DateTime(2028, 10, 26, 0, 0, 0, DateTimeKind.Utc)),
            Options
        );

        Assert.Contains("2028-10-26T00:00:00Z", json);
    }
}
