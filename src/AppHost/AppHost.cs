var builder = DistributedApplication.CreateBuilder(args);

var contexts = builder.AddParameter("contexts", "test", publishValueAsDefault: true);

var postgres = builder.AddPostgres("postgres").WithPgWeb();
var postgresDb = postgres.AddDatabase("pgdn", "app");
builder.AddRatchet("ratchet-postgres", postgresDb, "postgres", "migrations_postgres.json", contexts);

var mssql = builder.AddSqlServer("mssql");
var mssqlDb = mssql.AddDatabase("mssqldb", "app");
builder.AddRatchet("ratchet-mssql", mssqlDb, "mssql", "migrations_mssql.json", contexts);

var sqlite = builder.AddSqlite("sqlite").WithSqliteWeb();
builder.AddRatchet("ratchet-sqlite", sqlite, "sqlite", "migrations_sqlite.json", contexts);

builder.Build().Run();
