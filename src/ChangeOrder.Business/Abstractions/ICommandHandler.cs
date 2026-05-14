namespace ChangeOrder.Business.Abstractions;

/// <summary>
/// CQRS command-handler contract. One implementation per command (constitution
/// Principle II — feature-sliced CQRS).
/// </summary>
/// <typeparam name="TCommand">Command record type.</typeparam>
/// <typeparam name="TResult">Result type returned to the caller (typically <c>Result&lt;T, Error&gt;</c>).</typeparam>
public interface ICommandHandler<in TCommand, TResult>
{
    /// <summary>Executes the command and returns the result.</summary>
    /// <param name="command">Command instance.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the endpoint.</param>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
