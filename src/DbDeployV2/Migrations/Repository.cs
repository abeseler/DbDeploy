namespace DbDeploy.Migrations;

internal sealed class Repository(DbConnector dbConnector)
{
    private readonly DbConnector _dbConnector = dbConnector;
}
