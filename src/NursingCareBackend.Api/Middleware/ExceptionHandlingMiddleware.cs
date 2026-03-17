using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;

namespace NursingCareBackend.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IHostEnvironment _environment;
  private readonly ILogger<ExceptionHandlingMiddleware> _logger;

  public ExceptionHandlingMiddleware(
    RequestDelegate next,
    IHostEnvironment environment,
    ILogger<ExceptionHandlingMiddleware> logger)
  {
    _next = next;
    _environment = environment;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await _next(context);
    }
    catch (Exception ex)
    {
      await HandleExceptionAsync(context, ex);
    }
  }

  private async Task HandleExceptionAsync(HttpContext context, Exception exception)
  {
    var (statusCode, title) = exception switch
    {
      ArgumentNullException => (StatusCodes.Status400BadRequest, "Invalid request"),
      ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
      _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
    };
    var correlationId = context.GetCorrelationId();

    _logger.LogError(
      exception,
      "Unhandled exception for {Method} {Path}. StatusCode={StatusCode} CorrelationId={CorrelationId}",
      context.Request.Method,
      context.Request.Path,
      statusCode,
      correlationId);

    var problemDetails = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Detail = _environment.IsDevelopment() ? exception.Message : null,
      Instance = context.Request.Path
    };

    problemDetails.Extensions["correlationId"] = correlationId;

    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";

    await context.Response.WriteAsJsonAsync(problemDetails);
  }
}
