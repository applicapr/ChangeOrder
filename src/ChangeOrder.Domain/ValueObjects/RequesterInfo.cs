namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Agrupa la informacion del solitante del cambio
/// </summary>
public sealed record RequesterInfo(

    string Name,
    string Position,
    string Department,
    string Email

    );
