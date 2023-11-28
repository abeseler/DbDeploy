
namespace DbDeploy.Tests.Data;

public sealed class MigrationTests
{
    [Theory]
    [MemberData(nameof(ContextData))]
    public void IsMissingRequiredContext_ReturnsExpectedResult(string[] commandContexts, string[] migrationContexts, bool requireContext, bool expectedResult)
    {
        // Arrange
        var migration = new Migration
        {
            RequireContext = requireContext,
            ContextFilter = migrationContexts
        };

        // Act
        var result = migration.IsMissingRequiredContext(commandContexts);

        // Assert
        result.Should().Be(expectedResult);
    }

    public static IEnumerable<object[]> ContextData =>
        new List<object[]>
        {
            new object[] { Array.Empty<string>(), Array.Empty<string>(), false, false },
            new object[] { ContextOne, Array.Empty<string>(), false, false },
            new object[] { ContextOne, Array.Empty<string>(), true, false },
            new object[] { Array.Empty<string>(), ContextOne, false, false },
            new object[] { ContextOne, ContextOne, true, false },
            new object[] { ContextOne, MisMatchOne, true, true },
            new object[] { ContextTwo, ContextTwo, true, false },
            new object[] { ContextTwo, MisMatchTwo, true, true },
            new object[] { ContextThree, ContextThree, true, false },
            new object[] { ContextThree, MisMatchThree, true, true },
            new object[] { ContextTwo, ContextOne, true, false },
            new object[] { ContextThree, ContextOne, true, false },
            new object[] { ContextOne, ContextTwo, true, false },
            new object[] { ContextOne, ContextThree, true, false },

            new object[] { ContextOne, ContextOne, false, false },
            new object[] { ContextOne, MisMatchOne, false, true },
            new object[] { ContextOne, ContextTwo, false, false },
            new object[] { ContextOne, MisMatchTwo, false, true },
            new object[] { ContextOne, ContextThree, false, false },
            new object[] { ContextOne, MisMatchThree, false, true },
            new object[] { ContextOne, ContextOne, false, false },
            new object[] { ContextOne, ContextOne, false, false },
            new object[] { ContextOne, ContextTwo, false, false },
            new object[] { ContextOne, ContextThree, false, false },
        };

    private static string[] ContextOne => ["ctx1"];
    private static string[] ContextTwo => ["ctx1", "ctx2"];
    private static string[] ContextThree => ["ctx1", "ctx2", "ctx3"];
    private static string[] MisMatchOne => ["ctxA"];
    private static string[] MisMatchTwo => ["ctxA", "ctxB"];
    private static string[] MisMatchThree => ["ctxA", "ctxB", "ctxC"];
}
