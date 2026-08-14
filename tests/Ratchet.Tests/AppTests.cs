using System.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ratchet.Common;
using Ratchet.Data;
using Xunit;

namespace Ratchet.Tests;

public sealed class AppTests
{
    [Fact]
    public async Task RunAsync_PrintsUsageAndExitsWhenCommandIsMissing()
    {
        var previous = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            var settings = Options.Create(new Settings { Command = null });
            var repository = new Repository(new FailingDbProvider(), NullLogger<Repository>.Instance);
            var app = new App(repository, [], settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    [Fact]
    public async Task RunAsync_HelpCommandExitsZeroWithoutConnecting()
    {
        var previous = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 1;
            var settings = Options.Create(new Settings { Command = "help" });
            var repository = new Repository(new FailingDbProvider(), NullLogger<Repository>.Instance);
            var app = new App(repository, [], settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(0, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    [Fact]
    public async Task RunAsync_SetsExitCodeWhenDatabaseNeverConnects()
    {
        var previous = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            var settings = Options.Create(new Settings
            {
                Command = "update",
                ConnectionAttempts = 1,
                ConnectionRetryDelaySeconds = 0
            });
            var repository = new Repository(new FailingDbProvider(), NullLogger<Repository>.Instance);
            var app = new App(repository, [], settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    private sealed class FailingDbProvider : IDatabaseProvider
    {
        public Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("cannot connect");

        public Task<bool> TryAcquireSessionLock(IDbConnection connection, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task ReleaseSessionLock(IDbConnection connection, CancellationToken cancellationToken) => Task.CompletedTask;

        public string EnsureMigrationTablesExist => "";
        public string AcquireLock => "";
        public string ReleaseLock => "";
        public string GetAllMigrationHistories => "";
        public string InsertMigrationHistory => "";
        public string UpdateMigrationHistory => "";
    }
}
