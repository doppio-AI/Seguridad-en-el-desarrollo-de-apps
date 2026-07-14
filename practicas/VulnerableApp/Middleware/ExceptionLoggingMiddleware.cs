using Microsoft.AspNetCore.Mvc;

namespace VulnerableApp.Middleware;

public sealed class ExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionLoggingMiddleware> logger)
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
        catch (Exception exception)
        {
            var correlationId = context.TraceIdentifier;
            _logger.LogError(
                exception,
                "Excepcion no controlada en {Method} {Path} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                correlationId);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrio un error inesperado.",
                Detail = "Use el CorrelationId para localizar el evento en los registros."
            };
            problem.Extensions["correlationId"] = correlationId;

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
