using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var contexts = builder.AddParameter("contexts", "test", publishValueAsDefault: true);

var postgres = builder.AddPostgres("postgres").WithPgWeb();
var postgresDb = postgres.AddDatabase("pgdn", "app");

builder.AddRatchetForPostgres("deploy-postgres", postgresDb, contexts);

builder.Build().Run();
