using System.Text.RegularExpressions;
using Serilog.Context;

namespace VulnerableApp.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string TestRunHeaderName = "X-Test-Run-ID";
    public const string CorrelationIdItemKey = "CorrelationId";
    public const string TestRunIdItemKey = "TestRunId";

    private static readonly Regex SafeIdentifierPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetSafeHeader(context, HeaderName)
            ?? Guid.NewGuid().ToString("N");
        var testRunId = GetSafeHeader(context, TestRunHeaderName);

        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TestRunId", testRunId ?? "Manual"))
        {
            if (testRunId is not null)
            {
                context.Items[TestRunIdItemKey] = testRunId;
            }

            await _next(context);
        }
    }

    private static string? GetSafeHeader(HttpContext context, string headerName)
    {
        var value = context.Request.Headers[headerName].FirstOrDefault();
        return value is not null && SafeIdentifierPattern.IsMatch(value)
            ? value
            : null;
    }
}
