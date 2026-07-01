using DepRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.Ai;

/// <summary>
/// A deterministic, keyless feature-hashing embedder: it tokenizes text and hashes
/// tokens into a fixed-dimension, L2-normalized vector, so the RAG pipeline works with
/// no API key.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a fallback, not a semantic model.</b> Feature hashing measures overlap of
/// <em>literal tokens</em>, not meaning: two passages that say the same thing in
/// different words score as dissimilar. Retrieval quality is therefore closer to a
/// keyword match than to true semantic search. For production-grade RAG, register a
/// hosted embedding model behind <see cref="IEmbeddingGenerator"/>.
/// </para>
/// <para>
/// Uses a stable FNV-1a hash (not <see cref="string.GetHashCode()"/>, which is
/// randomized per process) so a query embedded in the API matches chunks embedded in
/// the Worker.
/// </para>
/// </remarks>
internal sealed class HashingEmbeddingGenerator : IEmbeddingGenerator
{
    /// <summary>Embedding dimensionality (the pgvector column width).</summary>
    public const int Dimensions = 256;

    public HashingEmbeddingGenerator(ILogger<HashingEmbeddingGenerator> logger) =>
        logger.LogWarning(
            "RAG semantic search is running on the deterministic hashing embedder (keyless fallback): " +
            "retrieval approximates lexical token overlap, not meaning. Register a hosted embedding model " +
            "behind IEmbeddingGenerator for production-grade semantic retrieval.");

    /// <inheritdoc />
    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var vector = new float[Dimensions];

        foreach (var token in Tokenize(text))
        {
            var hash = Fnv1A(token);
            var index = (int)(hash % Dimensions);
            // Signed hashing: half the tokens add, half subtract, reducing collisions.
            vector[index] += (hash & 1) == 0 ? 1f : -1f;
        }

        Normalize(vector);
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return text[start..i].ToLowerInvariant();
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return text[start..].ToLowerInvariant();
        }
    }

    private static uint Fnv1A(string token)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        var hash = offset;
        foreach (var c in token)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    private static void Normalize(float[] vector)
    {
        double sumOfSquares = 0;
        foreach (var value in vector)
        {
            sumOfSquares += value * value;
        }

        if (sumOfSquares <= 0)
        {
            return;
        }

        var magnitude = (float)Math.Sqrt(sumOfSquares);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
    }
}
