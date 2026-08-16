namespace Ratchet;

internal sealed record Error(string Message, Exception? Exception = null)
{
    public static readonly Error Uninitialized = new("Result was not initialized.");

    public override string ToString() => Message;

    public static Error From(Exception exception) => new(exception.Message, exception);
}
