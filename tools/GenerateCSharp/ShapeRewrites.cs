// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using NJsonSchema;

namespace GenerateCSharp;

/// <summary>
/// Rewrites the combinators NJsonSchema cannot map to C# types, after <see cref="EnumConstants.Strip"/>. Only the generated
/// types change; the JSON Schemas stay the validation authority.
/// </summary>
internal static class ShapeRewrites
{
    public static void Apply(JsonSchema root)
    {
        var all = Walk(root, []).ToList();

        // oneOf / anyOf branches that only constrain (required, not) carry no type: drop them.
        foreach (var schema in all)
        {
            RemoveConstraintOnly(schema.OneOf);
            RemoveConstraintOnly(schema.AnyOf);
        }

        foreach (var schema in all.Where(s => s.AnyOf.Count > 1))
        {
            CollapseAnyOf(schema);
        }
    }

    internal static IEnumerable<JsonSchema> Walk(JsonSchema schema, HashSet<JsonSchema> seen)
    {
        if (!seen.Add(schema))
        {
            yield break;
        }

        yield return schema;
        var children = schema.Properties.Values.Cast<JsonSchema>()
            .Concat(schema.Definitions.Values)
            .Concat(schema.AnyOf).Concat(schema.OneOf).Concat(schema.AllOf)
            .Concat(schema.Items)
            .Append(schema.Item).Append(schema.AdditionalPropertiesSchema).Append(schema.Not)
            .OfType<JsonSchema>();
        foreach (var child in children)
        {
            foreach (var nested in Walk(child, seen))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// anyOf [primitive, null] becomes a nullable primitive (a stripped enum would otherwise become an empty record);
    /// anyOf [object, null] becomes oneOf, the only form NJsonSchema reads as a nullable reference; an anyOf of several
    /// shapes (a number or an array, an object or a name) has no C# type and stays untyped (object).
    /// </summary>
    private static void CollapseAnyOf(JsonSchema schema)
    {
        var others = schema.AnyOf.Where(a => !IsNull(a)).Select(a => a.ActualSchema).ToList();
        if (others.Count > 1)
        {
            schema.AnyOf.Clear();
        }
        else if (others[0].Type is JsonObjectType.String or JsonObjectType.Integer or JsonObjectType.Number or JsonObjectType.Boolean)
        {
            schema.AnyOf.Clear();
            schema.Type = others[0].Type | JsonObjectType.Null;
        }
        else
        {
            foreach (var alternative in schema.AnyOf)
            {
                schema.OneOf.Add(alternative);
            }

            schema.AnyOf.Clear();
        }
    }

    private static void RemoveConstraintOnly(ICollection<JsonSchema> alternatives)
    {
        foreach (var alternative in alternatives.Where(IsConstraintOnly).ToList())
        {
            alternatives.Remove(alternative);
        }
    }

    private static bool IsConstraintOnly(JsonSchema schema) =>
        !schema.HasReference && schema.Type == JsonObjectType.None && schema.Properties.Count == 0
        && schema.Item is null && schema.Items.Count == 0 && schema.Enumeration.Count == 0;

    private static bool IsNull(JsonSchema schema) => schema.ActualSchema.Type == JsonObjectType.Null;
}
