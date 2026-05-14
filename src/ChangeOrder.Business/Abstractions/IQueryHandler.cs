namespace ChangeOrder.Business.Abstractions;

/// <summary>
/// CQRS query-handler contract. One implementation per query.
/// </summary>
/// <typeparam name="TQuery">Query record type.</typeparam>
/// <typeparam name="TResult">Projection returned to the caller.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
{
    /// <summary>Executes the query and returns its projection.</summary>
    /// <param name="query">Query instance.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the endpoint.</param>
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
