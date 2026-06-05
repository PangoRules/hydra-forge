namespace HydraForge.Domain.Common;

public class Result
{
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error != null) throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == null) throw new InvalidOperationException("Failure result must have an error.");
        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure { get; }

    public Error Error => _error ?? throw new InvalidOperationException("Success result has no error.");

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);
}

public class Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        if (isSuccess && value == null && error == null) throw new InvalidOperationException("Success result must have a value.");
        if (isSuccess && error != null) throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == null) throw new InvalidOperationException("Failure result must have an error.");
        IsSuccess = isSuccess;
        IsFailure = !isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure { get; }

    public T Value => _value ?? throw new InvalidOperationException("Failure result has no value.");

    public Error Error => _error ?? throw new InvalidOperationException("Success result has no error.");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(Error error) => new(false, default, error);
}