using ChangeOrder.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ChangeOrder.Presentation.Common;

/// <summary>
/// Single point of translation from <see cref="Error"/> codes to RFC 7807
/// <see cref="ProblemDetails"/> payloads. Evolve the mapping here; endpoints
/// should not contain ad-hoc status-code switches.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>Builds a <see cref="ProblemDetails"/> for the supplied domain error.</summary>
    public static ProblemDetails FromError(Error error, string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        int status = MapStatus(error.Code);
        ProblemDetails problem = new()
        {
            Type = $"https://changeorder.internal.example/errors/{error.Code}",
            Title = TitleFor(status),
            Status = status,
            Detail = error.Message,
            Instance = instance
        };
        problem.Extensions["code"] = error.Code;
        return problem;
    }

    private static int MapStatus(string code) => code switch
    {
        "order.not_found" => StatusCodes.Status404NotFound,
        "order.duplicate_number" => StatusCodes.Status409Conflict,
        "order.invalid_transition" => StatusCodes.Status409Conflict,
        "order.edit_after_draft" => StatusCodes.Status409Conflict,
        "order.daily_sequence_exhausted" => StatusCodes.Status409Conflict,
        "order.concurrency_conflict" => StatusCodes.Status409Conflict,
        "idempotency.payload_divergence" => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        _ => "Bad Request"
    };
}
