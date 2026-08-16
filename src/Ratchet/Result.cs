using System.Diagnostics.CodeAnalysis;

namespace Ratchet;

[DebuggerDisplay("{Succeeded ? \"Success\" : \"Failure\"}")]
internal readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly bool _succeeded;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _succeeded = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        _succeeded = false;
    }

    public bool Succeeded => _succeeded;
    public bool Failed => _succeeded is false;

    public void Deconstruct(out T? value, out Error? error)
    {
        value = _value;
        error = _succeeded ? null : ErrorOrUninitialized();
    }

    public bool TryGet([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? error)
    {
        if (_succeeded)
        {
            value = _value!;
            error = null;
            return true;
        }

        value = default;
        error = ErrorOrUninitialized();
        return false;
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        _succeeded ? onSuccess(_value!) : onFailure(ErrorOrUninitialized());

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);

    private Error ErrorOrUninitialized() => _error ?? Error.Uninitialized;
}
