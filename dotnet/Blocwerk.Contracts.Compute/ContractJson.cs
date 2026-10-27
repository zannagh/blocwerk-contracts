// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blocwerk.Contracts.Compute;

/// <summary>
/// Serializer options for the contract records. Absent and <c>null</c> mean the same everywhere in the protocol, and
/// several readers refuse an explicit <c>null</c> for an optional number (the textures options, for one), so nulls are
/// left out when writing.
/// </summary>
public static class ContractJson
{
    /// <summary>The options to read and write every record of this package with.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
