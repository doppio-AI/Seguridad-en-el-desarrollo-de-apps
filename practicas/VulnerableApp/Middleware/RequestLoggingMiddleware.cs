using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace VulnerableApp.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        await _next(context);
        stopwatch.Stop();

        var session = context.Features.Get<ISessionFeature>()?.Session;
        var username = session?.GetString("User")
            ?? context.User.Identity?.Name
            ?? "Anonimo";

        _logger.LogInformation(
            "HTTP {Method} {Path} respondio {StatusCode} en {ElapsedMs} ms | Usuario: {User} | IP: {IP} | CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            username,
            context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            context.TraceIdentifier);
    }
}
