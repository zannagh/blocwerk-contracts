// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using System.Text;
using NJsonSchema;

namespace GenerateCSharp;

/// <summary>
/// Turns the string enums of a schema into plain strings and renders their values as a constants class per named
/// <c>$defs</c> entry (<c>kind</c> becomes <c>KindValues</c>). A namespace gets each class once.
/// </summary>
internal static class EnumConstants
{
    /// <summary>Clears every string enum in <paramref name="root"/>; returns the named ones (by <c>$defs</c> key).</summary>
    public static SortedDictionary<string, string[]> Strip(JsonSchema root)
    {
        var named = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (name, def) in root.Definitions)
        {
            if (Values(def) is { } values)
            {
                named[name] = values;
            }
        }

        foreach (var schema in ShapeRewrites.Walk(root, []).Where(s => Values(s) is not null).ToList())
        {
            schema.Enumeration.Clear();
            schema.EnumerationNames.Clear();
        }

        return named;
    }

    /// <summary>The constants classes of <paramref name="constants"/> not yet rendered in <paramref name="ns"/>.</summary>
    public static string Render(SortedDictionary<string, string[]> constants, string ns, Dictionary<string, string> rendered, string relative)
    {
        var text = new StringBuilder();
        foreach (var (name, values) in constants)
        {
            var type = SchemaToCSharp.Pascal(name) + "Values";
            if (!rendered.TryAdd(type, relative))
            {
                continue;
            }

            text.Append('\n').Append("namespace ").Append(ns).Append('\n').Append("{\n");
            text.Append("    /// <summary>The values of <c>").Append(name).Append("</c>.</summary>\n");
            text.Append("    public static class ").Append(type).Append("\n    {\n");
            foreach (var value in values)
            {
                text.Append("        public const string ").Append(SchemaToCSharp.Pascal(value)).Append(" = \"").Append(value).Append("\";\n\n");
            }

            text.Length -= 1;
            text.Append("    }\n}\n");
        }

        return text.ToString();
    }

    private static string[]? Values(JsonSchema schema) =>
        schema.Enumeration.Count > 0 && schema.Enumeration.All(v => v is string or null) && schema.Enumeration.Any(v => v is string)
            ? schema.Enumeration.OfType<string>().ToArray()
            : null;
}
