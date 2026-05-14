using System.Globalization;
using System.Text.RegularExpressions;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.UpdateOrder;

/// <summary>
/// Manual validator for <see cref="UpdateOrderCommand"/>. Mirrors the
/// length/required/email rules used by the <c>Create</c> path so the
/// PUT contract stays consistent with <c>contracts/openapi.yaml</c>.
/// </summary>
public sealed partial class UpdateOrderValidator
{
    private const int ProgramNameMaxLength = 200;
    private const int ProductionVersionMaxLength = 50;
    private const int ScreenshotPathMaxLength = 500;
    private const int WorkDescriptionMaxLength = 2000;
    private const int RequestDetailsMaxLength = 4000;
    private const int JustificationMaxLength = 2000;
    private const int RequiredActionMaxLength = 1000;
    private const int RequesterNameMaxLength = 150;
    private const int RequesterPositionMaxLength = 100;
    private const int RequesterDepartmentMaxLength = 100;
    private const int RequesterEmailMaxLength = 200;

    private static readonly Regex EmailRegex = EmailPattern();

    /// <summary>Validates <paramref name="command"/>.</summary>
    public static Result<TVoid, Error> Validate(UpdateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<string> errors = new();

        if (command.RowVersion is null)
        {
            errors.Add("RowVersion is required.");
        }

        RequireText(errors, command.ProgramName, nameof(command.ProgramName), ProgramNameMaxLength);
        RequireText(errors, command.ProductionVersion, nameof(command.ProductionVersion), ProductionVersionMaxLength);
        OptionalText(errors, command.VersionScreenshotPath, nameof(command.VersionScreenshotPath), ScreenshotPathMaxLength);
        RequireText(errors, command.WorkDescription, nameof(command.WorkDescription), WorkDescriptionMaxLength);
        RequireText(errors, command.RequestDetails, nameof(command.RequestDetails), RequestDetailsMaxLength);
        RequireText(errors, command.Justification, nameof(command.Justification), JustificationMaxLength);
        RequireText(errors, command.RequiredAction, nameof(command.RequiredAction), RequiredActionMaxLength);
        RequireText(errors, command.RequesterName, nameof(command.RequesterName), RequesterNameMaxLength);
        RequireText(errors, command.RequesterPosition, nameof(command.RequesterPosition), RequesterPositionMaxLength);
        RequireText(errors, command.RequesterDepartment, nameof(command.RequesterDepartment), RequesterDepartmentMaxLength);
        RequireEmail(errors, command.RequesterEmail, nameof(command.RequesterEmail), RequesterEmailMaxLength);

        if (errors.Count == 0)
        {
            return Result<TVoid, Error>.Success(TVoid.Instance);
        }

        string message = string.Join("; ", errors);
        return Result<TVoid, Error>.Failure(new Error("validation.error", message));
    }

    private static void RequireText(List<string> errors, string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} is required.", field));
            return;
        }

        if (value.Length > maxLength)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} must not exceed {1} characters.", field, maxLength));
        }
    }

    private static void OptionalText(List<string> errors, string? value, string field, int maxLength)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length > maxLength)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} must not exceed {1} characters.", field, maxLength));
        }
    }

    private static void RequireEmail(List<string> errors, string? value, string field, int maxLength)
    {
        RequireText(errors, value, field, maxLength);
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
        {
            return;
        }

        if (!EmailRegex.IsMatch(value))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} must be a valid e-mail address.", field));
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
