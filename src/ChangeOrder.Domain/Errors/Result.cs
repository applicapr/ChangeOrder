using System.Diagnostics.CodeAnalysis;

namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Marker type used as the success payload when an operation has no return
/// value (e.g., state transitions, void-shaped operations). Singleton instance
/// available via <see cref="Instance"/>.
/// </summary>
public sealed record TVoid
{
    private TVoid() { }

    /// <summary>Singleton instance.</summary>
    public static TVoid Instance { get; } = new();
}

/// <summary>
/// Discriminated success/failure container for the Result Pattern
/// (Constitution Principle III — NON-NEGOTIABLE).
/// </summary>
/// <typeparam name="TValue">Successful payload type.</typeparam>
/// <typeparam name="TError">Failure payload type (typically <see cref="Error"/>).</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory pattern for the Result Pattern; the alternative non-generic helper class adds friction without runtime benefit.")]
public sealed record Result<TValue, TError>
{
    /// <summary>Successful payload. <c>null</c> when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public TValue? Value { get; }

    /// <summary>Failure payload. <c>null</c> when <see cref="IsSuccess"/> is <c>true</c>.</summary>
    public TError? Error { get; }

    /// <summary><c>true</c> for successes, <c>false</c> for failures.</summary>
    public bool IsSuccess { get; }

    /// <summary>Convenience inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    private Result(TValue? value, TError? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>Build a success carrying <paramref name="value"/>.</summary>
    public static Result<TValue, TError> Success(TValue value) => new(value, default, true);

    /// <summary>Build a failure carrying <paramref name="error"/>.</summary>
    public static Result<TValue, TError> Failure(TError error) => new(default, error, false);
}
