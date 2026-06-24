// Aspire orchestration: a single entry point that wires PostgreSQL, the Web API
// and the ingestion Worker, and expresses their startup ordering. Running this
// project (`dotnet run --project src/DepRadar.AppHost`) brings the whole system up.
var builder = DistributedApplication.CreateBuilder(args);

// Ephemeral Postgres for Slice 1 (clean state each run); pgAdmin gives a quick
// visual into the stored graph during a demo. A persistent data volume and the
// pgvector image (for changelog RAG) are introduced in later slices.
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var depRadarDb = postgres.AddDatabase("depradardb");

builder.AddProject<Projects.DepRadar_Api>("api")
    .WithReference(depRadarDb)
    .WaitFor(depRadarDb);

builder.AddProject<Projects.DepRadar_Worker>("worker")
    .WithReference(depRadarDb)
    .WaitFor(depRadarDb);

builder.Build().Run();
