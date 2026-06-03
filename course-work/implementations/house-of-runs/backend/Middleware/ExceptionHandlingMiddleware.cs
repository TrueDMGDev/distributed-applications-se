using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception.");

            var (statusCode, title) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid operation"),
                DbUpdateException => (StatusCodes.Status409Conflict, "Database update failed"),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
            };

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                    ? exception.Message
                    : "The request could not be completed.",
                Instance = context.Request.Path
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
