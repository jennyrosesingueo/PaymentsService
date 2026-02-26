using System.Net;
using System.Text.Json;
using PaymentsService.Core.Exceptions;
using PaymentsService.Core.Models;

namespace PaymentsService.API.Middleware;

/*
 * Global exception middleware that maps unhandled exceptions to consistent
 * HTTP error responses for the API.
 */
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DuplicatePaymentException ex)
        {
            // This should not normally reach here because idempotency returns 200,
            // but guard against any future flow changes.
            _logger.LogWarning(ex, "Duplicate payment attempted. ReferenceId={ReferenceId}", ex.ReferenceId);
            await WriteErrorAsync(context, HttpStatusCode.Conflict, "DUPLICATE_PAYMENT", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad argument in request");
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "INVALID_ARGUMENT", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred. Please try again later.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context, HttpStatusCode statusCode, string code, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var error = new ErrorResponse { Code = code, Message = message };
        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
