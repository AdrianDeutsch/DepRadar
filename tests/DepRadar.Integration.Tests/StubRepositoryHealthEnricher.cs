using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Integration.Tests;

/// <summary>No-op enricher so scan tests don't reach the live deps.dev/GitHub APIs.</summary>
internal sealed class StubRepositoryHealthEnricher : IRepositoryHealthEnricher
{
    public Task EnrichAsync(PackageId package, CancellationToken cancellationToken) => Task.CompletedTask;
}
