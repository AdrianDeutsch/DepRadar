// Aspire orchestration: a single entry point that wires PostgreSQL, the Web API
// and the ingestion Worker, and expresses their startup ordering. Running this
// project (`dotnet run --project src/DepRadar.AppHost`) brings the whole system up.
var builder = DistributedApplication.CreateBuilder(args);

// Ephemeral Postgres on the pgvector image (the changelog RAG store needs the
// `vector` extension); pgAdmin gives a quick visual into the stored graph.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithPgAdmin();

var depRadarDb = postgres.AddDatabase("depradardb");

builder.AddProject<Projects.DepRadar_Api>("api")
    .WithReference(depRadarDb)
    .WaitFor(depRadarDb);

builder.AddProject<Projects.DepRadar_Worker>("worker")
    .WithReference(depRadarDb)
    .WaitFor(depRadarDb);

builder.Build().Run();
