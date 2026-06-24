using DepRadar.Domain.Packages;

namespace DepRadar.Application.Scans;

/// <summary>API-facing read model for a scan run and its progress.</summary>
public sealed record ScanDto(
    Guid Id,
    string RootPackageId,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int PackagesDiscovered,
    int EdgesWritten,
    string? Error)
{
    /// <summary>Projects a domain <see cref="Scan"/> into a DTO.</summary>
    public static ScanDto FromDomain(Scan scan) => new(
        scan.Id.Value,
        scan.RootPackageId.Original,
        scan.Status.ToString(),
        scan.RequestedAt,
        scan.StartedAt,
        scan.CompletedAt,
        scan.PackagesDiscovered,
        scan.EdgesWritten,
        scan.Error);
}
