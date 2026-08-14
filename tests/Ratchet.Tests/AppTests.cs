using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ratchet.Commands;
using Ratchet.Common;
using Ratchet.Data;
using Ratchet.FileHandling;
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
            var app = new App(repository, UnusedResolver(), settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    [Fact]
    public async Task RunAsync_InvalidCommandDoesNotConnect()
    {
        var previous = Environment.ExitCode;
        var provider = new FailingDbProvider();
        try
        {
            Environment.ExitCode = 0;
            var settings = Options.Create(new Settings { Command = "foobar" });
            var repository = new Repository(provider, NullLogger<Repository>.Instance);
            var app = new App(repository, UnusedResolver(), settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(1, Environment.ExitCode);
            Assert.Equal(0, provider.ConnectCalls);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    [Fact]
    public async Task RunAsync_ValidateWithoutDatabaseDoesNotConnect()
    {
        var previous = Environment.ExitCode;
        var provider = new FailingDbProvider();
        try
        {
            Environment.ExitCode = 0;
            var settings = Options.Create(new Settings { Command = "validate" });
            var repository = new Repository(provider, NullLogger<Repository>.Instance);
            var app = new App(repository, ResolverForValidate(settings, repository), settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(0, provider.ConnectCalls);
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
            var app = new App(repository, UnusedResolver(), settings, NullLogger<App>.Instance);

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
            var app = new App(repository, UnusedResolver(), settings, NullLogger<App>.Instance);

            await app.RunAsync();

            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previous;
        }
    }

    private static CommandResolver UnusedResolver() =>
        new(new ServiceCollection().BuildServiceProvider());

    private static CommandResolver ResolverForValidate(IOptions<Settings> settings, Repository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton(repository);
        services.AddSingleton<ILogger<FileMigrationExtractor>>(NullLogger<FileMigrationExtractor>.Instance);
        services.AddSingleton<ILogger<ValidateCommand>>(NullLogger<ValidateCommand>.Instance);
        services.AddSingleton<FileMigrationExtractor>();
        services.AddSingleton<ValidateCommand>();
        return new CommandResolver(services.BuildServiceProvider());
    }

    private sealed class FailingDbProvider : IDatabaseProvider
    {
        public int ConnectCalls { get; private set; }

        public Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCalls++;
            throw new InvalidOperationException("cannot connect");
        }

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
