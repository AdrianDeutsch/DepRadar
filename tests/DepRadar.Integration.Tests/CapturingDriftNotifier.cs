using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Integration.Tests;

/// <summary>Test double that records the drift reports it would have sent and resolved.</summary>
internal sealed class CapturingDriftNotifier : IDriftNotifier
{
    private readonly List<DriftReport> _reports = [];
    private readonly List<string> _resolved = [];

    public IReadOnlyList<DriftReport> Reports => _reports;

    public IReadOnlyList<string> Resolved => _resolved;

    public Task NotifyAsync(DriftReport report, CancellationToken cancellationToken)
    {
        _reports.Add(report);
        return Task.CompletedTask;
    }

    public Task ResolveAsync(PackageId root, CancellationToken cancellationToken)
    {
        _resolved.Add(root.Value);
        return Task.CompletedTask;
    }
}
