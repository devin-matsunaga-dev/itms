using System.Diagnostics.CodeAnalysis;

namespace Itms.Platform.Results;

/// <summary>
/// The outcome of an operation that returns no value. Handlers return this and
/// endpoints translate it to HTTP, so that a failure the caller can act on never
/// travels as an exception (CONVENTIONS.md).
/// </summary>
public sealed class Result
{
    private Result(Error? error) => Error = error;

    /// <summary>True when the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>True when the operation failed and <see cref="Error"/> is populated.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>The failure, or <see langword="null"/> when the operation succeeded.</summary>
    public Error? Error { get; }

    /// <summary>A successful outcome.</summary>
    public static Result Success() => new(error: null);

    /// <summary>A failed outcome carrying <paramref name="error"/>.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <summary>A successful outcome carrying <paramref name="value"/>.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.FromValue(value);

    /// <summary>A failed outcome of a value-returning operation.</summary>
    public static Result<TValue> Failure<TValue>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Result<TValue>.FromError(error);
    }

    /// <summary>Lets a handler write <c>return error;</c> instead of <c>return Result.Failure(error);</c>.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Collapses both branches into one value, so callers cannot forget to handle the failure.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(Error);
    }
}
