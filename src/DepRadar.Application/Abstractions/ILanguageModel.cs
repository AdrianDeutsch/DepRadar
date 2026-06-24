namespace DepRadar.Application.Abstractions;

/// <summary>
/// The LLM seam. The default implementation is a no-op (returns <see langword="null"/>),
/// so the product works keyless; configuring an API key activates a real model
/// (e.g. Claude) without any call-site change.
/// </summary>
public interface ILanguageModel
{
    /// <summary>
    /// Completes the given system + user prompt, or returns <see langword="null"/> when
    /// no model is configured (the caller then falls back to a templated narrative).
    /// </summary>
    Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
