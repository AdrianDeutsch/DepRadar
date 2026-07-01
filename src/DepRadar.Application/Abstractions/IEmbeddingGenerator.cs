namespace DepRadar.Application.Abstractions;

/// <summary>
/// Turns text into an embedding vector for similarity search. The default
/// implementation is a deterministic, keyless local embedder that approximates
/// <em>lexical</em> overlap only; register a hosted embedding model behind this seam
/// for true semantic retrieval.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>Embeds a single piece of text.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
