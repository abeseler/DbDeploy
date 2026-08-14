using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithPgWeb();
var postgresDb = postgres.AddDatabase("pgdn", "app");

var mssql = builder.AddSqlServer("mssql");
var mssqlDb = mssql.AddDatabase("mssqldb", "app");

builder.AddProject<Ratchet>("deploy-postgres")
    .WithEnvironment("Deploy__Command", "update")
    .WithEnvironment("Deploy__StartingFile", "migrations_postgres.json")
    .WithEnvironment("Deploy__DatabaseProvider", "postgres")
    .WithEnvironment("Deploy__ConnectionString", postgresDb)
    .WithEnvironment("Deploy__ConnectionAttempts", "3")
    .WithEnvironment("Deploy__ConnectionRetryDelaySeconds", "5")
    .WithEnvironment("Serilog__MinimumLevel__Default", "Debug")
    .WithParentRelationship(postgresDb)
    .WithExplicitStart();

builder.AddProject<Ratchet>("deploy-mssql")
    .WithEnvironment("Deploy__Command", "update")
    .WithEnvironment("Deploy__StartingFile", "migrations_mssql.json")
    .WithEnvironment("Deploy__DatabaseProvider", "mssql")
    .WithEnvironment("Deploy__ConnectionString", mssqlDb)
    .WithEnvironment("Deploy__ConnectionAttempts", "3")
    .WithEnvironment("Deploy__ConnectionRetryDelaySeconds", "5")
    .WithEnvironment("Serilog__MinimumLevel__Default", "Debug")
    .WithParentRelationship(mssqlDb)
    .WithExplicitStart();

builder.Build().Run();
