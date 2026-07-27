using Serilog.Context;

namespace VulnerableApp.Middleware
{

	public class CorrelationIdMiddleware
	{
		private const string HeaderName = "X-Correlation-ID";
		private readonly RequestDelegate _next;

		public CorrelationIdMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var correlationId = Guid.NewGuid().ToString();

			context.Response.Headers[HeaderName] = correlationId;
			context.Items["CorrelationId"] = correlationId;

			using (LogContext.PushProperty("CorrelationId", correlationId))
			{
				await _next(context);
			}
		}
	}

	public static class CorrelationIdMiddlewareExtensions
	{
		public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
		{
			return builder.UseMiddleware<CorrelationIdMiddleware>();
		}
	}
}
