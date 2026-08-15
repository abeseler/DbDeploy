using Ratchet.Common;
using Xunit;

namespace Ratchet.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void IsDatabaseConfigured_RequiresAConnectionString_NotAProvider()
    {
        Assert.False(new Settings().IsDatabaseConfigured);
        Assert.False(new Settings { DatabaseProvider = "postgres" }.IsDatabaseConfigured);
        Assert.True(new Settings { ConnectionString = "Host=db" }.IsDatabaseConfigured);
    }

    [Fact]
    public void ResolveDatabaseProvider_DefaultsToPostgres()
    {
        Assert.Equal(Settings.DefaultDatabaseProvider, new Settings().ResolveDatabaseProvider());
        Assert.Equal("postgres", new Settings { DatabaseProvider = "" }.ResolveDatabaseProvider());
        Assert.Equal("mssql", new Settings { DatabaseProvider = "mssql" }.ResolveDatabaseProvider());
    }
}
