using Testcontainers.PostgreSql;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Spins up a real PostgreSQL container for the test class. Integration tests run
/// against actual Postgres — never an in-memory fake — so EF Core mappings, value
/// conversions and idempotent upserts are exercised the way they run in production.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgresFixture()
    {
        // Docker 29's API is incompatible with the Testcontainers Ryuk reaper on
        // this machine; disable it and rely on explicit DisposeAsync cleanup.
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

        // pgvector image: a superset of Postgres that ships the `vector` extension
        // needed by the changelog RAG store (Slice 4).
        _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .Build();
    }

    /// <summary>The connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(_container.StartAsync());

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
