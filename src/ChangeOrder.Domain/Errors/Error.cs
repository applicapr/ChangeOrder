namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Representa un error de negocio con código y mensaje descriptivo
/// </summary>

public sealed record Error(

    string Code,
    string Message

    );
