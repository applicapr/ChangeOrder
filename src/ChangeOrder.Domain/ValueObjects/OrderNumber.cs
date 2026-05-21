namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Encapsula el numero de orden con formato yyyyMMdd-##
/// </summary>
public sealed record OrderNumber
{
    ///<summary>
    ///Numero de orden formateado, ejemplo 20260511-01
    ///</summary>
    public string Value { get; }

    /// <summary>
    /// se inicializa la nueva propiedad de OrderNumber
    /// </summary>
    private OrderNumber(string value)
    {
        Value = value;
    }

    ///<summary>
    /// crea una nueva instancia de OrderNumber
    ///</summary>
    public static OrderNumber Create(DateTime date, int sequence)
    {
        return new($"{date:yyyyMMdd}-{sequence:D2}");
    }
}
