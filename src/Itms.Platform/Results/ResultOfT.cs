using System.Diagnostics.CodeAnalysis;

namespace Itms.Platform.Results;

/// <summary>
/// The outcome of an operation that produces a value. Deliberately not derived from
/// <see cref="Result"/>: keeping the two independent means both stay <c>sealed</c>
/// and neither can be widened by a subclass that changes what success means.
/// </summary>
/// <typeparam name="TValue">The value produced on success.</typeparam>
public sealed class Result<TValue>
{
    private readonly TValue _value;

    private Result(TValue value, Error? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>True when the operation succeeded and <see cref="Value"/> is safe to read.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>True when the operation failed and <see cref="Error"/> is populated.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    /// <summary>The failure, or <see langword="null"/> when the operation succeeded.</summary>
    public Error? Error { get; }

    /// <summary>
    /// The produced value. Reading it on a failed result is a programming error and
    /// throws — check <see cref="IsSuccess"/>, use <see cref="TryGetValue"/>, or
    /// <see cref="Match{TOut}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"Result is a failure ({Error.Code}); its value cannot be read.");

    internal static Result<TValue> FromValue(TValue value) => new(value, error: null);

    internal static Result<TValue> FromError(Error error) => new(default!, error);

    /// <summary>Lets a handler write <c>return ticket;</c> instead of <c>return Result.Success(ticket);</c>.</summary>
    public static implicit operator Result<TValue>(TValue value) => FromValue(value);

    /// <summary>Lets a handler write <c>return error;</c> instead of <c>return Result.Failure&lt;T&gt;(error);</c>.</summary>
    public static implicit operator Result<TValue>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FromError(error);
    }

    /// <summary>Reads the value without risking the <see cref="Value"/> throw.</summary>
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }

    /// <summary>Transforms a successful value, passing a failure through untouched.</summary>
    public Result<TOut> Map<TOut>(Func<TValue, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.FromValue(map(_value)) : Result<TOut>.FromError(Error);
    }

    /// <summary>Collapses both branches into one value, so callers cannot forget to handle the failure.</summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value) : onFailure(Error);
    }
}
