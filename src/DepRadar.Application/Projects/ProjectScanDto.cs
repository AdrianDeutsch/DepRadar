namespace DepRadar.Application.Projects;

/// <summary>The result of queuing a project scan: one scan per direct dependency.</summary>
public sealed record ProjectScanDto(int PackageCount, IReadOnlyList<QueuedPackageDto> Packages);

/// <summary>A queued per-package scan within a project scan.</summary>
public sealed record QueuedPackageDto(string PackageId, Guid ScanId);
