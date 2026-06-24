using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>
/// Maps <see cref="SpdxLicense"/> to/from its identifier. EF applies this only to
/// non-null values, so nullable license properties round-trip correctly.
/// </summary>
internal sealed class SpdxLicenseValueConverter()
    : ValueConverter<SpdxLicense, string>(
        license => license.Identifier,
        value => SpdxLicense.Create(value));
