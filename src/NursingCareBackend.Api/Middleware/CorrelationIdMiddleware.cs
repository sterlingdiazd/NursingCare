using Serilog.Context;

namespace NursingCareBackend.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
  public const string HeaderName = "X-Correlation-ID";
  private const string ItemKey = "CorrelationId";

  private readonly RequestDelegate _next;

  public CorrelationIdMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(correlationId))
    {
      correlationId = Guid.NewGuid().ToString("N");
    }

    context.Items[ItemKey] = correlationId;
    context.TraceIdentifier = correlationId;
    context.Response.Headers[HeaderName] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
      await _next(context);
    }
  }

  public static string GetCorrelationId(HttpContext context)
  {
    if (context.Items.TryGetValue(ItemKey, out var correlationId)
      && correlationId is string value
      && !string.IsNullOrWhiteSpace(value))
    {
      return value;
    }

    return context.TraceIdentifier;
  }
}
