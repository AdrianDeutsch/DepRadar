namespace DepRadar.Application.Exceptions;

/// <summary>Thrown when a scan id cannot be found. Surfaces as a 404 at the API.</summary>
public sealed class ScanNotFoundException(Guid scanId)
    : Exception($"Scan '{scanId}' was not found.")
{
    /// <summary>The scan id that could not be resolved.</summary>
    public Guid ScanId { get; } = scanId;
}
