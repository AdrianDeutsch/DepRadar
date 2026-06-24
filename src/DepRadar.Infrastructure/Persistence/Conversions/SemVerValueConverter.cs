using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>Maps <see cref="SemVer"/> to/from its canonical string form.</summary>
internal sealed class SemVerValueConverter()
    : ValueConverter<SemVer, string>(
        version => version.ToString(),
        value => SemVer.Parse(value));
