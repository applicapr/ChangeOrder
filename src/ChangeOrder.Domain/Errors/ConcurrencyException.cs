namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Excepción lanzada cuando hay un conflicto de concurrencia optimista.
/// </summary>
public sealed class ConcurrencyException : Exception
{
}
