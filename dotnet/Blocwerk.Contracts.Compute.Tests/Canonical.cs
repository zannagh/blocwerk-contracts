// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using System.Globalization;
using System.Text.Json;

namespace Blocwerk.Contracts.Compute.Tests;

/// <summary>
/// JSON equality as the protocol means it: absent equals null, numbers by value (2.0 equals 2), timestamps by instant,
/// object members in any order.
/// </summary>
internal static class Canonical
{
    public static bool Equal(JsonElement a, JsonElement b)
    {
        if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number)
        {
            return a.GetDouble() == b.GetDouble();
        }

        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.Object => ObjectsEqual(a, b),
            JsonValueKind.Array => a.GetArrayLength() == b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p => Equal(p.First, p.Second)),
            JsonValueKind.String => StringsEqual(a.GetString()!, b.GetString()!),
            _ => true,
        };
    }

    private static bool ObjectsEqual(JsonElement a, JsonElement b)
    {
        var left = Members(a);
        var right = Members(b);
        return left.Count == right.Count && left.All(m => right.TryGetValue(m.Key, out var other) && Equal(m.Value, other));
    }

    private static Dictionary<string, JsonElement> Members(JsonElement element) =>
        element.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => p.Value);

    private static bool StringsEqual(string a, string b) =>
        a == b
        || (DateTimeOffset.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.None, out var x)
            && DateTimeOffset.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.None, out var y)
            && x == y && a.Contains('T') && b.Contains('T'));
}
