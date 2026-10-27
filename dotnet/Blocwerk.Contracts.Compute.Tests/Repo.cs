// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using System.Text.Json;

namespace Blocwerk.Contracts.Compute.Tests;

/// <summary>The repository's schemas and fixtures, and the generated record of each schema (namespace by folder, name by title).</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    /// <summary>The schemas that describe payloads (zip layouts have no records).</summary>
    public static IEnumerable<string> PayloadSchemas() =>
        Directory.GetFiles(Path.Combine(Root, "schemas"), "*.schema.json", SearchOption.AllDirectories)
            .Where(p => !Key(p).StartsWith("layouts/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    /// <summary><c>…/schemas/runner/hello.schema.json</c> to <c>runner/hello</c>.</summary>
    public static string Key(string schemaPath) =>
        Path.GetRelativePath(Path.Combine(Root, "schemas"), schemaPath).Replace('\\', '/').Replace(".schema.json", string.Empty, StringComparison.Ordinal);

    public static Type RecordType(string key) => TryRecordType(key) ?? throw new InvalidOperationException($"No record for {key}.");

    public static Type? TryRecordType(string key)
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "schemas", key + ".schema.json")));
        var title = schema.RootElement.GetProperty("title").GetString();
        var folders = key.Split('/')[..^1].Select(Pascal);
        var name = string.Join('.', new[] { "Blocwerk.Contracts.Compute" }.Concat(folders)) + "." + title;
        return typeof(ContractJson).Assembly.GetType(name);
    }

    private static string Pascal(string name) =>
        string.Concat(name.Split('-', '_').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "schemas")) && Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root (schemas/, fixtures/) was not found above the test binaries.");
    }
}
