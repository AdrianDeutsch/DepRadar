using System.Text.Json;
using DepRadar.Domain.History;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>
/// Maps a snapshot's per-package risk states to/from a single JSON column. A snapshot
/// is an immutable, append-only record, so storing the states as one JSON document
/// keeps the schema flat (no child table) without losing queryability of the parent.
/// </summary>
internal sealed class PackageRiskStatesConverter()
    : ValueConverter<IReadOnlyList<PackageRiskState>, string>(
        states => JsonSerializer.Serialize(states, SerializerOptions),
        json => Deserialize(json))
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static List<PackageRiskState> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<PackageRiskState>>(json, SerializerOptions) ?? [];
}
