using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Exceptions;

/// <summary>
/// Thrown when a requested package cannot be found in any external metadata source.
/// The API surface translates this into a 404 response.
/// </summary>
public sealed class PackageNotFoundException(PackageId id)
    : Exception($"Package '{id}' was not found in any configured metadata source.")
{
    /// <summary>The package id that could not be resolved.</summary>
    public PackageId PackageId { get; } = id;
}
