using BuildingBlocks.Domain;
using FluentValidation;

namespace ApiHost;

/// <summary>
/// Produces safe 400 responses for command validation failures. It deliberately
/// does not expose exception stacks or persistence details to API callers.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            logger.LogInformation("Request validation failed for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteBadRequestAsync(context, "Request validation failed.", exception.Errors.Select(error => error.ErrorMessage).Distinct().ToArray());
        }
        catch (DomainException exception)
        {
            logger.LogInformation("Domain request rejected for {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, exception.Message);
            await WriteBadRequestAsync(context, exception.Message, []);
        }
    }

    private static async Task WriteBadRequestAsync(HttpContext context, string message, IReadOnlyCollection<string> errors)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("Cannot write an API error response after the response has started.");

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = message, errors });
    }
}
