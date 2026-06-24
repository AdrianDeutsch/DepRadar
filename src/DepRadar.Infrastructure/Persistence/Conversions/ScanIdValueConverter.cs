using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>Maps <see cref="ScanId"/> to/from its underlying GUID.</summary>
internal sealed class ScanIdValueConverter()
    : ValueConverter<ScanId, Guid>(
        id => id.Value,
        value => ScanId.From(value));
