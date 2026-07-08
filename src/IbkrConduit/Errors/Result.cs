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
    private readonly bool _isSuccess;

    // §6.6 (ERR-5): distinguishes a value/error-carrying Result (set by both private constructors) from
    // the uninitialized `default(Result<T>)` / `new Result<T>()` state, which carries neither. False for
    // the uninitialized state (a struct's field defaults) and true for every constructed Result.
    private readonly bool _initialized;

    /// <summary>
    /// Whether the operation succeeded. Throws <see cref="InvalidOperationException"/> if this is an
    /// uninitialized <c>default(Result&lt;T&gt;)</c> — which carries neither a value nor an error and is
    /// invalid to consume (§6.6).
    /// </summary>
    public bool IsSuccess => _initialized
        ? _isSuccess
        : throw UninitializedResult();

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if the result is a failure or is
    /// uninitialized (§6.6).
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed Result. Check IsSuccess first.");

    /// <summary>
    /// The error details. Throws <see cref="InvalidOperationException"/> if the result is a success or is
    /// uninitialized (§6.6).
    /// </summary>
    public IbkrError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful Result. Check IsSuccess first.");

    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
        _initialized = true;
    }

    private Result(IbkrError error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
        _initialized = true;
    }

    // §6.6 (ERR-5): a member access on an uninitialized Result names the misuse at the misuse site rather
    // than surfacing a null-reference failure downstream.
    private static InvalidOperationException UninitializedResult() => new(
        "Uninitialized Result — a default(Result<T>) or new Result<T>() carries neither a value nor an " +
        "error and is invalid to consume. Construct it via Result<T>.Success(...) or Result<T>.Failure(...).");

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
