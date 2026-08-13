using Xunit;
using DbDeploy.Common;
using DbDeploy.Models;

namespace DbDeploy.Tests;

public sealed class MigrationOrderResolverTests
{
    private static Migration NewMigration(string fileName, string title, string[]? dependsOn = null, string[]? contextFilter = null, bool contextRequired = false) => new()
    {
        FileName = fileName,
        Title = title,
        SqlStatements = ["select 1;"],
        DependsOn = dependsOn ?? [],
        ContextFilter = contextFilter ?? [],
        ContextRequired = contextRequired
    };

    private static List<string> OrderedIds(Result<List<Migration>> result)
    {
        var (ordered, error) = result;
        Assert.Null(error);
        Assert.NotNull(ordered);
        return ordered!.Select(m => m.Id).ToList();
    }

    private static Result<List<Migration>> Resolve(List<Migration> migrations, string[] contexts, string[]? applied = null) =>
        MigrationOrderResolver.Resolve(migrations, contexts, applied ?? []);

    [Fact]
    public void Resolve_PreservesInsertionOrder_WhenNoDependencies()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Tables/a.sql", "a"),
            NewMigration("Tables/b.sql", "b"),
            NewMigration("Tables/c.sql", "c")
        };

        var ids = OrderedIds(Resolve(migrations, []));

        Assert.Equal(["Tables/a.sql [a]", "Tables/b.sql [b]", "Tables/c.sql [c]"], ids);
    }

    [Fact]
    public void Resolve_ReordersToSatisfyDependency()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/orders_fk.sql", "fk", dependsOn: ["Tables/orders.sql"]),
            NewMigration("Tables/orders.sql", "orders")
        };

        var ids = OrderedIds(Resolve(migrations, []));

        Assert.Equal(["Tables/orders.sql [orders]", "Fks/orders_fk.sql [fk]"], ids);
    }

    [Fact]
    public void Resolve_NormalizesReferenceSeparatorsAndLeadingSlashAndCase()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["\\TABLES\\orders.sql"]),
            NewMigration("Tables/orders.sql", "orders")
        };

        var ids = OrderedIds(Resolve(migrations, []));

        Assert.Equal(["Tables/orders.sql [orders]", "Fks/fk.sql [fk]"], ids);
    }

    [Fact]
    public void Resolve_ExpandsFileReferenceToAllBlocksInTargetFile()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Views/v.sql", "v", dependsOn: ["Tables/orders.sql"]),
            NewMigration("Tables/orders.sql", "orders:create"),
            NewMigration("Tables/orders.sql", "orders:addColumn")
        };

        var ids = OrderedIds(Resolve(migrations, []));

        Assert.Equal(2, ids.IndexOf("Views/v.sql [v]"));
        Assert.True(ids.IndexOf("Tables/orders.sql [orders:create]") < ids.IndexOf("Views/v.sql [v]"));
        Assert.True(ids.IndexOf("Tables/orders.sql [orders:addColumn]") < ids.IndexOf("Views/v.sql [v]"));
    }

    [Fact]
    public void Resolve_ReturnsNotFound_WhenReferenceDoesNotMatchAnyFile()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/missing.sql"])
        };

        var (ordered, error) = Resolve(migrations, []);

        Assert.Null(ordered);
        Assert.NotNull(error);
        Assert.Contains("Tables/missing.sql", error!.Message);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguous_WhenReferenceMatchesMultipleFilesCaseInsensitively()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/orders.sql"]),
            NewMigration("Tables/orders.sql", "lower"),
            NewMigration("Tables/Orders.sql", "upper")
        };

        var (ordered, error) = Resolve(migrations, []);

        Assert.Null(ordered);
        Assert.NotNull(error);
        Assert.Contains("more than one", error!.Message);
    }

    [Fact]
    public void Resolve_ReturnsFilteredOut_WhenDependencyExcludedByContext()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/orders.sql"]),
            NewMigration("Tables/orders.sql", "orders", contextFilter: ["prod"])
        };

        var (ordered, error) = Resolve(migrations, ["dev"]);

        Assert.Null(ordered);
        Assert.NotNull(error);
        Assert.Contains("excluded by the active context", error!.Message);
    }

    [Fact]
    public void Resolve_ReturnsCycle_WhenDependenciesFormLoop()
    {
        var migrations = new List<Migration>
        {
            NewMigration("a.sql", "a", dependsOn: ["c.sql"]),
            NewMigration("b.sql", "b", dependsOn: ["a.sql"]),
            NewMigration("c.sql", "c", dependsOn: ["b.sql"])
        };

        var (ordered, error) = Resolve(migrations, []);

        Assert.Null(ordered);
        Assert.NotNull(error);
        Assert.Contains("cycle", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_IgnoresDependenciesDeclaredByFilteredOutMigration()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Tables/orders.sql", "orders"),
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/missing.sql"], contextFilter: ["prod"])
        };

        var ids = OrderedIds(Resolve(migrations, ["dev"]));

        Assert.Equal(["Tables/orders.sql [orders]", "Fks/fk.sql [fk]"], ids);
    }

    [Fact]
    public void Resolve_SuppressesNotFound_WhenReferencedFileWasAlreadyApplied()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/orders.sql"])
        };

        var ids = OrderedIds(Resolve(migrations, [], applied: ["Tables/orders.sql"]));

        Assert.Equal(["Fks/fk.sql [fk]"], ids);
    }

    [Fact]
    public void Resolve_StillErrors_WhenReferencedFileNeitherPresentNorApplied()
    {
        var migrations = new List<Migration>
        {
            NewMigration("Fks/fk.sql", "fk", dependsOn: ["Tables/orders.sql"])
        };

        var (ordered, error) = Resolve(migrations, [], applied: ["Tables/other.sql"]);

        Assert.Null(ordered);
        Assert.NotNull(error);
        Assert.Contains("Tables/orders.sql", error!.Message);
    }
}
