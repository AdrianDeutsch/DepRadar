using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;

namespace DepRadar.Application.Projects;

/// <summary>
/// Handles <see cref="RequestProjectScanCommand"/>: parses the project file and queues
/// a scan per direct dependency by re-dispatching <see cref="RequestScanCommand"/>.
/// </summary>
public sealed class RequestProjectScanHandler(ISender sender)
    : IRequestHandler<RequestProjectScanCommand, ProjectScanDto>
{
    /// <inheritdoc />
    public async Task<ProjectScanDto> Handle(RequestProjectScanCommand request, CancellationToken cancellationToken)
    {
        var packages = ProjectFileParser.ParseDirectPackages(request.Content);
        if (packages.Count == 0)
        {
            throw new ArgumentException("No PackageReference entries were found in the project file.");
        }

        var queued = new List<QueuedPackageDto>(packages.Count);
        foreach (var package in packages)
        {
            var scan = await sender.Send(new RequestScanCommand(package), cancellationToken);
            queued.Add(new QueuedPackageDto(scan.RootPackageId, scan.Id));
        }

        return new ProjectScanDto(queued.Count, queued);
    }
}
