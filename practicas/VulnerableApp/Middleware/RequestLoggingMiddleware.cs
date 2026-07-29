using System.Diagnostics;

namespace VulnerableApp.Middleware
{
    
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

 public async Task InvokeAsync(HttpContext context)
{
    var sw = Stopwatch.StartNew();

    await _next(context);

    sw.Stop();

    var level = GetLogLevel(context.Response.StatusCode);

    _logger.Log(
        level,
        "Request {Metodo} {Ruta} respondió {StatusCode} en {DuracionMs} ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        sw.ElapsedMilliseconds);
}

private static LogLevel GetLogLevel(int statusCode)
{
    if (statusCode >= 500) return LogLevel.Error;
    if (statusCode >= 400) return LogLevel.Warning;
    return LogLevel.Information;
}
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}
