namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Centraliza todos los errores de negocio del dominio
/// </summary>
public static class DomainErrors
{
    /// <summary>
    /// Errores relacionados con las órdenes de cambio
    /// </summary>
    public static class Order
    {
        /// <summary>
        /// La orden de cambio no fue encontrada
        /// </summary>
        public static readonly Error NotFound = new("Order.NotFound", "La orden de cambio no fue encontrada.");

        /// <summary>
        /// Ya existe una orden con ese número
        /// </summary>
        public static readonly Error AlreadyExists = new("Order.AlreadyExists", "Ya existe una orden con ese número.");

        /// <summary>
        /// Los datos de la orden no son válidos.
        /// </summary>
        public static readonly Error ValidationFailed = new("Order.ValidationFailed", "Los datos enviados no son válidos.");
    }
}
