using DbDeploy.FileHandling;

namespace DbDeploy.Tests.FileHandling;

public sealed class YamlFileParserTests
{
    [Fact]
    public void Parse_WhenYamlFileIsValid_ReturnsMigrations()
    {
        // Arrange
        var yaml = """
            - include-all: Tables/
            - include-all: Views/
            """;

        // Act
        var result = YamlFileParser.Parse(yaml, "test.yaml");

        // Assert

    }
}
