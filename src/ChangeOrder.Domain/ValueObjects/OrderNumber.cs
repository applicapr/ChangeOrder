using System.Globalization;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Identifier of a <c>ChangeOrder</c> formatted as <c>yyyyMMdd-##</c>
/// (e.g., <c>20260512-01</c>). The two-digit suffix is the daily sequence
/// in the range [1..99]. Construction is closed: the only entry point is
/// <see cref="Create(DateOnly, int)"/>.
/// </summary>
public sealed record OrderNumber
{
    /// <summary>Underlying canonical string representation.</summary>
    public string Value { get; }

    private OrderNumber(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Builds a validated <see cref="OrderNumber"/> for a given UTC date and
    /// daily sequence. Returns <c>DomainErrors.Order.DailySequenceExhausted</c>
    /// when <paramref name="sequence"/> is outside the closed range [1..99].
    /// </summary>
    /// <param name="date">UTC date used as prefix.</param>
    /// <param name="sequence">Daily sequence, 1-99 inclusive.</param>
    public static Result<OrderNumber, Error> Create(DateOnly date, int sequence)
    {
        if (sequence is < 1 or > 99)
        {
            return Result<OrderNumber, Error>.Failure(DomainErrors.Order.DailySequenceExhausted(date));
        }

        string formatted = string.Format(
            CultureInfo.InvariantCulture,
            "{0:yyyyMMdd}-{1:00}",
            date,
            sequence);

        return Result<OrderNumber, Error>.Success(new OrderNumber(formatted));
    }

    /// <summary>
    /// Rehydrates an <see cref="OrderNumber"/> from a value previously stored
    /// by the persistence layer. Intended exclusively for the EF Core value
    /// converter — DO NOT call from application code.
    /// </summary>
    public static OrderNumber FromPersistence(string value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value;
}
