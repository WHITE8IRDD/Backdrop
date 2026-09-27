// Backdrop.Core — Result<T>: never throw across process boundary (ARCHITECTURE.md §7).
namespace Backdrop.Core;

/// <summary>Discriminated success/failure without exceptions.</summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Error { get; }

    private Result(bool success, T? value, string error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, string.Empty);
    public static Result<T> Fail(string error) => new(false, default, error);
}

/// <summary>Void result.</summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }

    private Result(bool success, string error)
    {
        IsSuccess = success;
        Error = error;
    }

    public static Result Ok() => new(true, string.Empty);
    public static Result Fail(string error) => new(false, error);
}
