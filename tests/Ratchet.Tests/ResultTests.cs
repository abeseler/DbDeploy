using Xunit;
using Ratchet;

namespace Ratchet.Tests;

public sealed class ResultTests
{
    [Fact]
    public void ImplicitValue_IsSuccess()
    {
        Result<int> result = 7;

        Assert.True(result.Succeeded);
        Assert.True(result.TryGet(out var value, out var error));
        Assert.Equal(7, value);
        Assert.Null(error);
    }

    [Fact]
    public void ImplicitError_IsFailure()
    {
        Result<int> result = Errors.FileIsEmpty;

        Assert.True(result.Failed);
        Assert.False(result.TryGet(out var value, out var error));
        Assert.Equal(0, value);
        Assert.Equal(Errors.FileIsEmpty.Message, error.Message);
    }

    [Fact]
    public void Default_IsFailure()
    {
        var result = default(Result<string>);

        Assert.True(result.Failed);
        Assert.False(result.TryGet(out var value, out var error));
        Assert.Null(value);
        Assert.Equal(Error.Uninitialized.Message, error.Message);
    }

    [Fact]
    public void Deconstruct_ExposesValueOrError()
    {
        Result<string> ok = "yes";
        var (value, error) = ok;
        Assert.Equal("yes", value);
        Assert.Null(error);

        Result<string> failed = Errors.FileIsEmpty;
        var (failedValue, failedError) = failed;
        Assert.Null(failedValue);
        Assert.Equal(Errors.FileIsEmpty.Message, failedError!.Message);
    }

    [Fact]
    public void Match_SelectsTheActiveBranch()
    {
        Result<int> ok = 3;
        Assert.Equal("3", ok.Match(v => v.ToString(), e => e.Message));

        Result<int> failed = Errors.FileIsEmpty;
        Assert.Equal(Errors.FileIsEmpty.Message, failed.Match(_ => "nope", e => e.Message));
    }

    [Fact]
    public void From_KeepsTheExceptionMessage()
    {
        var error = Error.From(new InvalidOperationException("boom"));

        Assert.Equal("boom", error.Message);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }
}
