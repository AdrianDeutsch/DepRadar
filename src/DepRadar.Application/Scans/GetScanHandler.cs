using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Scans;

/// <summary>Handles <see cref="GetScanQuery"/>: reads scan status for polling.</summary>
public sealed class GetScanHandler(IScanRepository scanRepository)
    : IRequestHandler<GetScanQuery, ScanDto?>
{
    /// <inheritdoc />
    public async Task<ScanDto?> Handle(GetScanQuery request, CancellationToken cancellationToken)
    {
        var scan = await scanRepository.GetAsync(ScanId.From(request.ScanId), cancellationToken);
        return scan is null ? null : ScanDto.FromDomain(scan);
    }
}
