using ChangeOrder.Domain.Errors;
using Microsoft.AspNetCore.Http;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>
/// Extensiones para convertir errores de dominio al formato ProblemDetails (RFC 7807).
/// </summary>
public static class ErrorExtensions
{
    /// <summary>
    /// Convierte un <see cref="Error"/> de dominio en una respuesta <see cref="IResult"/>
    /// con formato ProblemDetails (RFC 7807), mapeando el código de error al HTTP status code correcto.
    /// </summary>
    /// <param name="error">El error de dominio a convertir.</param>
    /// <param name="httpContext">El contexto HTTP actual, utilizado para obtener la ruta de instancia.</param>
    public static IResult ToProblemDetails(this Error error, HttpContext httpContext)
    {
        int statusCode = error.Code switch
        {
            "Order.NotFound"               => StatusCodes.Status404NotFound,
            "Order.AlreadyExists"          => StatusCodes.Status409Conflict,
            "Order.ConcurrencyConflict"    => StatusCodes.Status409Conflict,
            "Order.ValidationFailed"       => StatusCodes.Status422UnprocessableEntity,
            "Order.IdempotencyKeyConflict" => StatusCodes.Status422UnprocessableEntity,
            _                              => StatusCodes.Status400BadRequest
        };

        return TypedResults.Problem(
            detail: error.Message,
            instance: httpContext.Request.Path,
            statusCode: statusCode,
            title: error.Code,
            type: "https://tools.ietf.org/html/rfc7807",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code
            });
    }
}
