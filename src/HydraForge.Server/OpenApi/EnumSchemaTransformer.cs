using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HydraForge.Server.OpenApi;

// AddOpenApi() has no built-in awareness of the global JsonStringEnumConverter
// (registered in Program.cs), so it documented every enum as `type: integer`
// while the server actually serialized them as their string name. That mismatch
// broke NSwag-generated clients — Newtonsoft.Json throws trying to deserialize
// "Owner" into an int field. This transformer makes the schema match the real
// wire format for every enum in the API surface.
public class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var underlyingType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (underlyingType.IsEnum)
        {
            var isNullable = schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Null);
            schema.Type = isNullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
            schema.Format = null;
            schema.Enum = Enum.GetNames(underlyingType).Select(name => (JsonNode)name).ToList();

            // A numeric default (e.g. from [DefaultValue] or a C# default enum member)
            // is now the wrong type for a string-typed schema — rewrite it to the
            // matching enum name instead of leaving a dangling int default.
            if (schema.Default is JsonValue defaultValue && defaultValue.TryGetValue<int>(out var defaultInt))
            {
                schema.Default = (JsonNode?)Enum.GetName(underlyingType, defaultInt);
            }
        }

        return Task.CompletedTask;
    }
}
