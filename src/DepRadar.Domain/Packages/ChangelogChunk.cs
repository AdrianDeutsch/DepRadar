using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Packages;

/// <summary>
/// A chunk of changelog/release text for a package version, with its embedding for
/// similarity search. The embedding is a plain <see cref="float"/> array so the
/// Domain stays free of any vector-store dependency; Infrastructure maps it to a
/// pgvector column.
/// </summary>
public sealed class ChangelogChunk
{
    // Parameterless constructor for the persistence layer (EF Core) only.
    private ChangelogChunk()
    {
    }

    private ChangelogChunk(PackageId packageId, SemVer version, int ordinal, string text, float[] embedding)
    {
        PackageId = packageId;
        Version = version;
        Ordinal = ordinal;
        Text = text;
        Embedding = embedding;
    }

    /// <summary>The package this chunk belongs to.</summary>
    public PackageId PackageId { get; private set; }

    /// <summary>The version the chunk describes.</summary>
    public SemVer Version { get; private set; } = null!;

    /// <summary>Ordinal of the chunk within the version's notes (0-based).</summary>
    public int Ordinal { get; private set; }

    /// <summary>The chunk text (untrusted external content).</summary>
    public string Text { get; private set; } = null!;

    /// <summary>The embedding vector for similarity search.</summary>
    public float[] Embedding { get; private set; } = [];

    /// <summary>Creates a changelog chunk.</summary>
    public static ChangelogChunk Create(PackageId packageId, SemVer version, int ordinal, string text, float[] embedding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(embedding);
        return new ChangelogChunk(packageId, version, ordinal, text.Trim(), embedding);
    }
}
