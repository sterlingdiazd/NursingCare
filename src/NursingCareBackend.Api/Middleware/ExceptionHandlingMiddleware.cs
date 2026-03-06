using Microsoft.AspNetCore.Mvc;

namespace NursingCareBackend.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IHostEnvironment _environment;

  public ExceptionHandlingMiddleware(
    RequestDelegate next,
    IHostEnvironment environment)
  {
    _next = next;
    _environment = environment;
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

    var problemDetails = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Detail = _environment.IsDevelopment() ? exception.ToString() : null,
      Instance = context.Request.Path
    };

    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";

    await context.Response.WriteAsJsonAsync(problemDetails);
  }
}


