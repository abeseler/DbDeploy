using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Ratchet.Data;
using Ratchet.Models;
using Xunit;

namespace Ratchet.Tests;

public sealed class RepositoryTests : IDisposable
{
    static RepositoryTests() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ratchet-tests-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task ApplyMigration_AssignsMonotonicSequenceAcrossDeployments()
    {
        {
            var repo = await OpenRepository();
            Assert.True((await repo.ApplyMigration(Ok("a.sql", "first"), null)).Succeeded);
            Assert.True((await repo.ApplyMigration(Ok("b.sql", "second"), null)).Succeeded);

            var firstRun = await repo.GetAllMigrationHistories();
            Assert.Equal(1, firstRun["a.sql [first]"].ExecutedSequence);
            Assert.Equal(2, firstRun["b.sql [second]"].ExecutedSequence);
        }

        {
            var repo = await OpenRepository();
            Assert.True((await repo.ApplyMigration(Ok("c.sql", "third"), null)).Succeeded);

            var secondRun = await repo.GetAllMigrationHistories();
            Assert.Equal(1, secondRun["a.sql [first]"].ExecutedSequence);
            Assert.Equal(2, secondRun["b.sql [second]"].ExecutedSequence);
            Assert.Equal(3, secondRun["c.sql [third]"].ExecutedSequence);
        }
    }

    [Fact]
    public async Task ApplyMigration_PreservesSequenceOnReapply()
    {
        var repo = await OpenRepository();
        var migration = Ok("views.sql", "view", runOnChange: true);

        Assert.True((await repo.ApplyMigration(migration, null)).Succeeded);
        var original = (await repo.GetAllMigrationHistories())[migration.Id];
        Assert.Equal(1, original.ExecutedSequence);

        var changed = migration with { Hash = "changed", SqlStatements = ["SELECT 2;"] };
        Assert.True((await repo.ApplyMigration(changed, original)).Succeeded);

        var updated = (await repo.GetAllMigrationHistories())[migration.Id];
        Assert.Equal(1, updated.ExecutedSequence);
        Assert.Equal("changed", updated.Hash);
        Assert.Equal(2, repo.MigrationsApplied);
    }

    [Fact]
    public async Task ApplyMigration_SkipDoesNotCountAsAppliedOrConsumeSequence()
    {
        var repo = await OpenRepository();

        var skipped = await repo.ApplyMigration(Failing("skip.sql", "skip", Migration.ErrorHandling.Skip), null);
        Assert.True(skipped.Succeeded);
        Assert.Equal(0, repo.MigrationsApplied);
        Assert.Equal(1, repo.MigrationsSkipped);
        Assert.Equal(0, repo.MigrationsMarked);
        Assert.Empty(await repo.GetAllMigrationHistories());

        Assert.True((await repo.ApplyMigration(Ok("ok.sql", "ok"), null)).Succeeded);
        Assert.Equal(1, repo.MigrationsApplied);
        Assert.Equal(1, repo.MigrationsSkipped);

        var histories = await repo.GetAllMigrationHistories();
        Assert.Single(histories);
        Assert.Equal(1, histories["ok.sql [ok]"].ExecutedSequence);
    }

    [Fact]
    public async Task ApplyMigration_MarkRecordsHistoryAndDoesNotCountAsApplied()
    {
        var repo = await OpenRepository();

        var marked = await repo.ApplyMigration(Failing("mark.sql", "mark", Migration.ErrorHandling.Mark), null);
        Assert.True(marked.Succeeded);
        Assert.Equal(0, repo.MigrationsApplied);
        Assert.Equal(0, repo.MigrationsSkipped);
        Assert.Equal(1, repo.MigrationsMarked);

        var histories = await repo.GetAllMigrationHistories();
        Assert.Single(histories);
        Assert.Equal(1, histories["mark.sql [mark]"].ExecutedSequence);
    }

    [Fact]
    public async Task ApplyMigration_FailDoesNotRecordOrCountAsApplied()
    {
        var repo = await OpenRepository();

        var failed = await repo.ApplyMigration(Failing("fail.sql", "fail", Migration.ErrorHandling.Fail), null);
        Assert.True(failed.Failed);
        Assert.Equal(0, repo.MigrationsApplied);
        Assert.Equal(0, repo.MigrationsSkipped);
        Assert.Equal(0, repo.MigrationsMarked);
        Assert.Empty(await repo.GetAllMigrationHistories());
    }

    private async Task<Repository> OpenRepository()
    {
        var repo = new Repository(new SqliteDbProvider($"Data Source={_dbPath}"), NullLogger<Repository>.Instance);
        await repo.EnsureMigrationTablesExist();
        await repo.GetAllMigrationHistories();
        return repo;
    }

    private static Migration Ok(string fileName, string title, bool runOnChange = false) => new()
    {
        FileName = fileName,
        Title = title,
        SqlStatements = ["SELECT 1;"],
        Hash = title,
        ContextFilter = [],
        RunOnChange = runOnChange,
        RunInTransaction = true,
        Timeout = 30
    };

    private static Migration Failing(string fileName, string title, Migration.ErrorHandling onError) => new()
    {
        FileName = fileName,
        Title = title,
        SqlStatements = ["THIS IS NOT VALID SQL;"],
        Hash = title,
        ContextFilter = [],
        RunInTransaction = true,
        Timeout = 30,
        OnError = onError
    };
}
