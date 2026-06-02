namespace ChangeOrder.Domain.Errors;

/// <summary>
/// 
/// </summary>

public sealed record Result<TValue, TError>
{
    /// <summary>
    /// 
    /// </summary>

    public bool IsSuccess { get; private init; }

    /// <summary>
    /// 
    /// </summary>

    public TValue? Value { get; private init; }

    /// <summary>
    /// 
    /// </summary>

    public TError? Error { get; private init; }

    private Result() { }

    /// <summary>
    /// 
    /// </summary>

    public static Result<TValue, TError> Success(TValue value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>
    /// 
    /// </summary>
    
    public static Result<TValue, TError> Failure(TError error) =>
        new() { IsSuccess = false, Error = error };

}
