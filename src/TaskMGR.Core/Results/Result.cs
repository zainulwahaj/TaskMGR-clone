using System;
namespace TaskMGR.Core.Results;

public readonly struct Result<T, TError>
{
    private readonly object? _value;
    private readonly object? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = default;
    }

    private Result(TError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? _value is T value
            ? value
            : throw new InvalidOperationException("Result does not contain a valid value.")
        : throw new InvalidOperationException("Result does not contain a value.");

    public TError Error => !IsSuccess
        ? _error is TError error
            ? error
            : throw new InvalidOperationException("Result does not contain a valid error.")
        : throw new InvalidOperationException("Result does not contain an error.");

    public static Result<T, TError> Ok(T value) => new(value);

    public static Result<T, TError> Fail(TError error) => new(error);
}
