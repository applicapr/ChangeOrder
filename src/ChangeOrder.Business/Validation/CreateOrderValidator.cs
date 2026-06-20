using System.Text.RegularExpressions;
using ChangeOrder.Business.Commands.CreateOrder;

namespace ChangeOrder.Business.Validation;

/// <summary>
/// Validador para CreateOrderCommand.
/// </summary>
public static partial class CreateOrderValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Valida los datos del command. Retorna una lista de errores —
    /// vacía si el command es válido.
    /// </summary>
    public static List<string> Validate(CreateOrderCommand command)
    {
        List<string> errors = new();

        if (string.IsNullOrWhiteSpace(command.ProgramName))
            errors.Add("ProgramName es requerido.");

        if (command.ProgramName?.Length > 200)
            errors.Add("ProgramName no puede exceder 200 caracteres.");

        if (string.IsNullOrWhiteSpace(command.ProductionVersion))
            errors.Add("ProductionVersion es requerido.");

        if (string.IsNullOrWhiteSpace(command.RequesterEmail) ||
            !EmailRegex().IsMatch(command.RequesterEmail))
            errors.Add("RequesterEmail no tiene un formato válido.");

        return errors;
    }
}
