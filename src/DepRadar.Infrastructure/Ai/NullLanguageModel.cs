using DepRadar.Application.Abstractions;

namespace DepRadar.Infrastructure.Ai;

/// <summary>
/// Default <see cref="ILanguageModel"/> used when no API key is configured: returns
/// <see langword="null"/> so callers fall back to a deterministic templated narrative.
/// Keeps DepRadar fully functional out of the box.
/// </summary>
internal sealed class NullLanguageModel : ILanguageModel
{
    /// <inheritdoc />
    public Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
