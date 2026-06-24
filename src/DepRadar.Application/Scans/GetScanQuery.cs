using DepRadar.Application.Messaging;

namespace DepRadar.Application.Scans;

/// <summary>Query: read a scan's current status. Returns <see langword="null"/> if unknown.</summary>
/// <param name="ScanId">The scan id to read.</param>
public sealed record GetScanQuery(Guid ScanId) : IRequest<ScanDto?>;
