namespace DbDeploy.Common;

[DebuggerDisplay("{DebugDisplay()}")]
public readonly struct Result<TValue, TError> where TValue : class where TError : class
{
    private readonly TValue? _value;
    private readonly TError? _error;

    private Result(TValue value) => _value = value;
    private Result(TError error) => _error = error;

    public bool IsSuccess => _value is not null;
    public bool IsFailure => IsSuccess is false;

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public static implicit operator Result<TValue, TError>(TValue value) => new(value);
    public static implicit operator Result<TValue, TError>(TError error) => new(error);

    private string DebugDisplay() => IsSuccess ? $"Success: {_value}" : $"Failure: {_error}";
}

public sealed record Success(string? Message = null)
{
    public static Success Default { get; } = new();
    public bool HasMessage => string.IsNullOrWhiteSpace(Message) is false;
}

public sealed record Error(string? Message = null)
{
    public static Error Default { get; } = new();
    public bool HasMessage => string.IsNullOrWhiteSpace(Message) is false;
}
