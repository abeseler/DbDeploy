using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Ratchet;
using Ratchet.Journal;
using Ratchet.Models;
using Xunit;

namespace Ratchet.Tests;

public sealed class JournalTests : IDisposable
{
    static JournalTests() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ratchet-tests-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Apply_AssignsMonotonicSequenceAcrossDeployments()
    {
        {
            var journal = await OpenJournal();
            Assert.True((await journal.Apply(Ok("a.sql", "first"), null)).Succeeded);
            Assert.True((await journal.Apply(Ok("b.sql", "second"), null)).Succeeded);

            var firstRun = await journal.GetHistories();
            Assert.Equal(1, firstRun["a.sql [first]"].ExecutedSequence);
            Assert.Equal(2, firstRun["b.sql [second]"].ExecutedSequence);
            Assert.Same(firstRun["a.sql [first]"], firstRun["A.SQL [FIRST]"]);
        }

        {
            var journal = await OpenJournal();
            Assert.True((await journal.Apply(Ok("c.sql", "third"), null)).Succeeded);

            var secondRun = await journal.GetHistories();
            Assert.Equal(1, secondRun["a.sql [first]"].ExecutedSequence);
            Assert.Equal(2, secondRun["b.sql [second]"].ExecutedSequence);
            Assert.Equal(3, secondRun["c.sql [third]"].ExecutedSequence);
        }
    }

    [Fact]
    public async Task Apply_PreservesSequenceOnReapply()
    {
        var journal = await OpenJournal();
        var migration = Ok("views.sql", "view", run: Migration.RunMode.OnChange);

        Assert.True((await journal.Apply(migration, null)).Succeeded);
        var original = (await journal.GetHistories())[migration.Id];
        Assert.Equal(1, original.ExecutedSequence);

        var changed = migration with { Hash = "changed", SqlStatements = ["SELECT 2;"] };
        Assert.True((await journal.Apply(changed, original)).Succeeded);
        Assert.Equal(migration.Hash, original.Hash);

        var updated = (await journal.GetHistories())[migration.Id];
        Assert.Equal(1, updated.ExecutedSequence);
        Assert.Equal("changed", updated.Hash);
        Assert.Single(await journal.GetHistories());
    }

    [Fact]
    public async Task Apply_SkipDoesNotCountAsAppliedOrConsumeSequence()
    {
        var journal = await OpenJournal();

        var skipped = await journal.Apply(Failing("skip.sql", "skip", Migration.ErrorHandling.Skip), null);
        Assert.True(skipped.TryGet(out var skipOutcome, out _));
        Assert.Equal(ApplyOutcome.Skipped, skipOutcome);
        Assert.Empty(await journal.GetHistories());

        Assert.True((await journal.Apply(Ok("ok.sql", "ok"), null)).Succeeded);

        var histories = await journal.GetHistories();
        Assert.Single(histories);
        Assert.Equal(1, histories["ok.sql [ok]"].ExecutedSequence);
    }

    [Fact]
    public async Task Apply_MarkRecordsHistoryAndDoesNotCountAsApplied()
    {
        var journal = await OpenJournal();

        var marked = await journal.Apply(Failing("mark.sql", "mark", Migration.ErrorHandling.Mark), null);
        Assert.True(marked.TryGet(out var markOutcome, out _));
        Assert.Equal(ApplyOutcome.Marked, markOutcome);

        var histories = await journal.GetHistories();
        Assert.Single(histories);
        Assert.Equal(1, histories["mark.sql [mark]"].ExecutedSequence);
    }

    [Fact]
    public async Task Baseline_RecordsHashWithoutSequence()
    {
        var journal = await OpenJournal();
        var migration = Ok("old.sql", "old");

        await journal.Baseline(migration, null);
        var history = (await journal.GetHistories())[migration.Id];

        Assert.Equal("old", history.Hash);
        Assert.Null(history.ExecutedSequence);
    }

    [Fact]
    public async Task Repair_UpdatesHashAndPreservesSequence()
    {
        var journal = await OpenJournal();
        var original = Ok("t.sql", "t");
        Assert.True((await journal.Apply(original, null)).Succeeded);
        var history = (await journal.GetHistories())[original.Id];
        Assert.Equal(1, history.ExecutedSequence);

        var edited = original with { Hash = "edited" };
        var hashBeforeRepair = history.Hash;
        await journal.Repair(edited, history);
        Assert.Equal(hashBeforeRepair, history.Hash);

        var repaired = (await journal.GetHistories())[original.Id];
        Assert.Equal("edited", repaired.Hash);
        Assert.Equal(1, repaired.ExecutedSequence);
    }

    [Fact]
    public async Task Apply_FailDoesNotRecordOrCountAsApplied()
    {
        var journal = await OpenJournal();

        var failed = await journal.Apply(Failing("fail.sql", "fail", Migration.ErrorHandling.Fail), null);
        Assert.True(failed.Failed);
        Assert.Empty(await journal.GetHistories());
    }

    private async Task<MigrationJournal> OpenJournal()
    {
        var journal = new MigrationJournal(new SqliteDbProvider($"Data Source={_dbPath}"), NullLogger<MigrationJournal>.Instance);
        await journal.EnsureTables();
        await journal.GetHistories();
        return journal;
    }

    private static Migration Ok(string fileName, string title, Migration.RunMode run = Migration.RunMode.Once) => new()
    {
        FileName = fileName,
        Title = title,
        SqlStatements = ["SELECT 1;"],
        Hash = title,
        ContextFilter = [],
        Run = run,
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
