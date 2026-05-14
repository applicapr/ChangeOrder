using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ChangeOrder.Domain.Errors;

namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// Manual validator for <see cref="CreateOrderCommand"/>. Encodes the
/// length/required/email rules from <c>data-model.md §3</c> and
/// <c>contracts/openapi.yaml</c> without taking a third-party dependency.
/// </summary>
/// <remarks>
/// The Business layer cannot rely on the
/// <c>Microsoft.Extensions.Validation</c> attribute-based pipeline because
/// that surface lives in <c>Microsoft.AspNetCore.Http</c> and would couple
/// Business to ASP.NET Core (constitution P-I). Plain manual composition is
/// the substitute.
/// </remarks>
public sealed partial class CreateOrderValidator
{
    private const int IdempotencyKeyMinLength = 8;
    private const int IdempotencyKeyMaxLength = 64;
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

    /// <summary>
    /// Validates <paramref name="command"/>. Returns
    /// <c>Result.Success(TVoid.Instance)</c> on success or a single combined
    /// <see cref="Error"/> with code <c>validation.error</c> on failure.
    /// </summary>
    public static Result<TVoid, Error> Validate(CreateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<string> errors = new();

        RequireText(errors, command.IdempotencyKey, nameof(command.IdempotencyKey), IdempotencyKeyMaxLength, IdempotencyKeyMinLength);
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

    private static void RequireText(List<string> errors, string? value, string field, int maxLength, int minLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} is required.", field));
            return;
        }

        if (value.Length < minLength)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, "{0} must be at least {1} characters.", field, minLength));
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
