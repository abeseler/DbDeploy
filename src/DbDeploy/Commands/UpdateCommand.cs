using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class UpdateCommand(FileMigrationExtractor extrator, Repository repo, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "update";
    private readonly List<Migration> MigrationToSync = [];
    private readonly List<Migration> MigrationToExecute = [];
    private readonly List<Migration> MigrationSkipped = [];

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var migrations = extrator.ExtractAll();
        var migrationHistories = await repo.GetAllMigrationHistories();

        return Errors.CommandNotImplemented;
    }
}
