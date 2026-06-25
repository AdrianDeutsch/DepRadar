using DepRadar.Application.Analysis;
using DepRadar.Application.Messaging;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Diff;

/// <summary>
/// Handles <see cref="GetUpgradeDiffQuery"/>: analyzes both versions with the
/// stateless analyzer and diffs the results.
/// </summary>
public sealed class GetUpgradeDiffHandler(ProjectAnalyzer analyzer)
    : IRequestHandler<GetUpgradeDiffQuery, UpgradeDiff?>
{
    /// <inheritdoc />
    public async Task<UpgradeDiff?> Handle(GetUpgradeDiffQuery request, CancellationToken cancellationToken)
    {
        if (!SemVer.TryParse(request.FromVersion, out var fromVersion)
            || !SemVer.TryParse(request.ToVersion, out var toVersion))
        {
            return null;
        }

        PackageId package;
        try
        {
            package = PackageId.Create(request.PackageId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var fromGraph = await analyzer.AnalyzeAsync(package, fromVersion, cancellationToken);
        var toGraph = await analyzer.AnalyzeAsync(package, toVersion, cancellationToken);
        if (fromGraph is null || toGraph is null)
        {
            return null;
        }

        return GraphDiffer.Diff(fromGraph, toGraph);
    }
}
