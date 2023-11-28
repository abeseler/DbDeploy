using DbDeploy.FileHandling;

namespace DbDeploy.Tests.FileHandling;

public sealed class SqlFileParserTests
{
    [Fact]
    public void ParseText_ReturnsExpectedResult()
    {
        // Arrange
        var text = """
            /* Migration title=CreateTable key1=value1 key2=value2 */
            SET ANSI_NULLS ON;
            GO
            SET QUOTED_IDENTIFIER GO ON;
            GO

            CREATE TABLE dbo.ExampleTable
            (
            	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
            );
            GO
            
            /* Migration
                title=CreateTableTwo
            	key3=value3
            	key99=value99
            */
            CREATE TABLE dbo.ExampleTableTwo
            (
            	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
            );
            
            /* Migration title = Create Table Three key1=value1 key2=value2
            	key3="value3 with space"
            	key99=value99 with space no quotes
            */
            CREATE TABLE dbo.ExampleTableThree
            (
            	ExampleTableId INT NOT NULL IDENTITY(1,1) PRIMARY KEY
            );

            /* Migration title=CreateTableBad key1=value1 key2=value2 */
            """;

        // Act
        var result = SqlFileParser.Parse(text, "Example.sql");

        // Assert
    }
}
