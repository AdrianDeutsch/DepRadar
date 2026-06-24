using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Deterministic <see cref="IPackageMetadataSource"/> used in integration tests so
/// the only real external dependency is Postgres (no network, no API quota).
/// Returns two versions for every requested package.
/// </summary>
internal sealed class StubMetadataSource : IPackageMetadataSource
{
    public Task<PackageMetadata?> GetAsync(PackageId id, CancellationToken cancellationToken) =>
        Task.FromResult<PackageMetadata?>(new PackageMetadata(
            PackageId: id.Value,
            Description: "Stubbed package metadata.",
            ProjectUrl: new Uri("https://www.newtonsoft.com/json"),
            SourceRepositoryUrl: new Uri("https://github.com/JamesNK/Newtonsoft.Json"),
            License: "MIT",
            IsDeprecated: false,
            LatestStableVersion: "13.0.3",
            Versions:
            [
                new PackageVersionMetadata("13.0.3", new DateTimeOffset(2023, 3, 8, 7, 42, 54, TimeSpan.Zero), false, "MIT"),
                new PackageVersionMetadata("12.0.3", new DateTimeOffset(2019, 11, 9, 0, 0, 0, TimeSpan.Zero), false, null),
            ]));
}
