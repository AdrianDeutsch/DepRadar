namespace DepRadar.Application.Abstractions;

/// <summary>
/// Turns text into an embedding vector for similarity search. The default
/// implementation is a deterministic, keyless local embedder; a hosted embedding
/// model can be swapped in behind this seam.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Embeds a single piece of text.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
