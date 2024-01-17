﻿using System.Data;
using System.Data.SqlClient;

namespace DbDeploy.Migrations;

internal sealed class DbConnector(IOptions<Settings> options)
{
    private readonly string? _connectionString = new SqlConnectionStringBuilder(options.Value.ConnectionString).ConnectionString;

    public async Task<IDbConnection> ConnectAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}