using DepRadar.Application.History;
using DepRadar.Domain.History;

namespace DepRadar.Integration.Tests;

/// <summary>Test double that records the drift reports it would have sent.</summary>
internal sealed class CapturingDriftNotifier : IDriftNotifier
{
    private readonly List<DriftReport> _reports = [];

    public IReadOnlyList<DriftReport> Reports => _reports;

    public Task NotifyAsync(DriftReport report, CancellationToken cancellationToken)
    {
        _reports.Add(report);
        return Task.CompletedTask;
    }
}
