using DbDeploy.Data;

namespace DbDeploy.Tests.Data;

public sealed class MigrationHistoryTests
{
    [Fact]
    public void GetKey_ReturnsSameValue_WhenMigrationHasSameFileNameAndTitle()
    {
        // Arrange
        var migration = new Migration
        {
            FileName = "ExampleFile.sql",
            Title = "Test"
        };

        var sut = new MigrationHistory
        {
            FileName = migration.FileName,
            Title = migration.Title
        };

        // Act
        var result = sut.GetKey();

        // Assert
        result.Should().Be(migration.GetKey());
    }

    [Fact]
    public void GetKey_ReturnsDifferentValue_WhenMigrationHasDifferentFileName()
    {
        // Arrange
        var migration = new Migration
        {
            FileName = "ExampleFile.sql",
            Title = "Test"
        };

        var sut = new MigrationHistory
        {
            FileName = "DifferentFile.sql",
            Title = migration.Title
        };

        // Act
        var result = sut.GetKey();

        // Assert
        result.Should().NotBe(migration.GetKey());
    }

    [Fact]
    public void GetKey_ReturnsDifferentValue_WhenMigrationHasDifferentTitle()
    {
        // Arrange
        var migration = new Migration
        {
            FileName = "ExampleFile.sql",
            Title = "Test"
        };

        var sut = new MigrationHistory
        {
            FileName = migration.FileName,
            Title = "Different Title"
        };

        // Act
        var result = sut.GetKey();

        // Assert
        result.Should().NotBe(migration.GetKey());
    }
}
