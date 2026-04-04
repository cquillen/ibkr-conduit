namespace IbkrConduit.Errors;

/// <summary>
/// Represents the outcome of an IBKR API call — either a success value or an error.
/// Readonly struct for zero-allocation on the success path.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly IbkrError? _error;

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if the result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result. Check IsSuccess first.");

    /// <summary>
    /// The error details. Throws <see cref="InvalidOperationException"/> if the result is a success.
    /// </summary>
    public IbkrError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result. Check IsSuccess first.");

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(IbkrError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Creates a successful result.</summary>
#pragma warning disable CA1000 // Static factory methods on generic types are the standard Result pattern
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result.</summary>
    public static Result<T> Failure(IbkrError error) => new(error);
#pragma warning restore CA1000

    /// <summary>
    /// Returns this result if successful, or throws <see cref="IbkrApiException"/> wrapping the error.
    /// </summary>
    public Result<T> EnsureSuccess() => IsSuccess
        ? this
        : throw new IbkrApiException(_error!);

    /// <summary>Transforms the success value, preserving errors.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector) => IsSuccess
        ? Result<TOut>.Success(selector(_value!))
        : Result<TOut>.Failure(_error!);

    /// <summary>Calls the appropriate action based on success or failure.</summary>
    public void Switch(Action<T> onSuccess, Action<IbkrError> onError)
    {
        if (IsSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onError(_error!);
        }
    }

    /// <summary>Calls the appropriate function and returns its result.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IbkrError, TOut> onError) => IsSuccess
        ? onSuccess(_value!)
        : onError(_error!);
}
