using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>
/// Maps <see cref="PackageId"/> to/from its normalized string form for storage.
/// Rehydration skips validation since stored ids are already normalized.
/// </summary>
internal sealed class PackageIdValueConverter()
    : ValueConverter<PackageId, string>(
        id => id.Value,
        value => PackageId.FromNormalized(value));
