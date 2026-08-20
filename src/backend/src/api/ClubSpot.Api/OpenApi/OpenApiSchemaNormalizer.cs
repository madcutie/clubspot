using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClubSpot.Api.OpenApi;

// Shapes the generator emits that are true but unusable downstream, fixed here rather than in each
// frontend, which would be fixing the same thing twice.
internal static class OpenApiSchemaNormalizer
{
    public static void Apply(OpenApiOptions options)
    {
        options.AddSchemaTransformer((schema, _, _) =>
        {
            // An enum arrives with its values and no type at all, which generates as `unknown`.
            if (schema.Enum is { Count: > 0 })
            {
                var nullable = schema.Enum.Any(value => value is null) ? JsonSchemaType.Null : default;
                schema.Enum = [.. schema.Enum.Where(value => value is not null)];
                schema.Type = JsonSchemaType.String | nullable;
                return Task.CompletedTask;
            }

            // A number arrives as integer-or-string because the web defaults accept a quoted number
            // on input. Every response writes a real number and no client of ours sends a quoted
            // one, so the union only buys `number | string` on every numeric field of both frontends.
            if (schema.Type is { } type && type.HasFlag(JsonSchemaType.String)
                && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
            {
                schema.Type = type & ~JsonSchemaType.String;
                schema.Pattern = null;
            }

            return Task.CompletedTask;
        });

        // A route token the handler never takes as an argument is invisible to the generator, and a
        // document that names a placeholder it does not declare is invalid. The portal reads
        // {clubSlug} from an endpoint filter, so every one of its routes lands here.
        options.AddOperationTransformer((operation, context, _) =>
        {
            foreach (var name in PathTokens(context.Description.RelativePath))
            {
                if (operation.Parameters?.Any(parameter => parameter.Name == name) == true) continue;
                operation.Parameters ??= [];
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = name,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                });
            }

            return Task.CompletedTask;
        });
    }

    private static IEnumerable<string> PathTokens(string? relativePath)
    {
        if (relativePath is null) yield break;
        var rest = relativePath;
        while (rest.IndexOf('{') is var open && open >= 0)
        {
            rest = rest[(open + 1)..];
            var close = rest.IndexOf('}');
            if (close < 0) yield break;
            var token = rest[..close];
            // Route constraints and defaults are not part of the name: {id:guid}, {page=0}.
            var end = token.IndexOfAny([':', '=', '?']);
            yield return end < 0 ? token : token[..end];
            rest = rest[(close + 1)..];
        }
    }
}
