using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Scans;

/// <summary>Handles <see cref="RequestScanCommand"/>: creates and queues a scan.</summary>
public sealed class RequestScanHandler(
    IScanRepository scanRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RequestScanCommand, ScanDto>
{
    /// <inheritdoc />
    public async Task<ScanDto> Handle(RequestScanCommand request, CancellationToken cancellationToken)
    {
        var rootId = PackageId.Create(request.PackageId);
        var scan = Scan.Create(rootId, timeProvider.GetUtcNow());

        await scanRepository.AddAsync(scan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ScanDto.FromDomain(scan);
    }
}
