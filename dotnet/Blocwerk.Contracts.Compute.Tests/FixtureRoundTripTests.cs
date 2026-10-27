// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using System.Text.Json;

namespace Blocwerk.Contracts.Compute.Tests;

/// <summary>Every valid golden fixture reads into its generated record and writes back to the same JSON (nulls dropped).</summary>
public class FixtureRoundTripTests
{
    public static TheoryData<string, string> Fixtures()
    {
        var data = new TheoryData<string, string>();
        foreach (var schema in Repo.PayloadSchemas())
        {
            var key = Repo.Key(schema);
            foreach (var fixture in Directory.GetFiles(Path.Combine(Repo.Root, "fixtures", key, "valid"), "*.json").Order(StringComparer.Ordinal))
            {
                data.Add(key, Path.GetFileName(fixture));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_RoundTripsThroughItsRecord(string key, string fixture)
    {
        var type = Repo.RecordType(key);
        var json = File.ReadAllText(Path.Combine(Repo.Root, "fixtures", key, "valid", fixture));

        var record = JsonSerializer.Deserialize(json, type, ContractJson.Options);
        var written = JsonSerializer.Serialize(record, type, ContractJson.Options);

        using var expected = JsonDocument.Parse(json);
        using var actual = JsonDocument.Parse(written);
        Assert.True(Canonical.Equal(expected.RootElement, actual.RootElement), $"{key}/{fixture} wrote:\n{written}");
    }

    [Fact]
    public void EveryPayloadSchema_HasARecord()
    {
        var missing = Repo.PayloadSchemas().Select(Repo.Key).Where(k => Repo.TryRecordType(k) is null).ToList();

        Assert.Empty(missing);
    }
}
