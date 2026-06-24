using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace DepRadar.Infrastructure.Persistence.Conversions;

/// <summary>
/// Maps the domain's plain <see cref="float"/>[] embedding to/from a pgvector
/// <see cref="Vector"/>, keeping the vector-store type out of the Domain.
/// </summary>
internal sealed class EmbeddingVectorConverter()
    : ValueConverter<float[], Vector>(
        embedding => new Vector(embedding),
        vector => vector.ToArray());
