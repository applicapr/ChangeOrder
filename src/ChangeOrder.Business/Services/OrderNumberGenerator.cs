using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Services;

/// <summary>
/// Computes the next available <see cref="OrderNumber"/> for a given UTC date.
/// Delegates the locked-read (<c>UPDLOCK + HOLDLOCK</c>) to the repository
/// (research.md R-1) and wraps the result in the daily-sequence guard exposed
/// by <see cref="OrderNumber.Create(DateOnly, int)"/>.
/// </summary>
/// <remarks>
/// Per R-1 the UNIQUE-violation retry loop lives in the command handler, since
/// only the handler owns the final <see cref="IUnitOfWork.SaveChangesAsync"/>
/// call. This service exposes a small per-attempt API the handler can call
/// repeatedly within its own retry loop.
/// </remarks>
public sealed class OrderNumberGenerator
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>Builds a generator bound to the given repository abstraction.</summary>
    public OrderNumberGenerator(IChangeOrderRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Returns the next <see cref="OrderNumber"/> for <paramref name="dateUtc"/>.
    /// Surfaces <c>DomainErrors.Order.DailySequenceExhausted</c> when the
    /// next sequence would fall outside the closed range [1..99].
    /// </summary>
    /// <param name="dateUtc">UTC date that prefixes the order number.</param>
    /// <param name="cancellationToken">Token propagated to the repository call.</param>
    public async Task<Result<OrderNumber, Error>> GenerateAsync(
        DateOnly dateUtc,
        CancellationToken cancellationToken)
    {
        Result<int, Error> sequenceResult = await _repository
            .GetNextSequenceForDateAsync(dateUtc, cancellationToken)
            .ConfigureAwait(false);

        if (sequenceResult.IsFailure)
        {
            // R-1 C1: propagate retryable failures (e.g. order.deadlock_victim)
            // verbatim so the command-layer retry loop can decide whether to
            // re-attempt under a fresh transaction.
            return Result<OrderNumber, Error>.Failure(sequenceResult.Error!);
        }

        return OrderNumber.Create(dateUtc, sequenceResult.Value!);
    }
}
