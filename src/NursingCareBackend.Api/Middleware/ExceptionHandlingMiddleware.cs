using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.ErrorHandling;
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
    var (statusCode, title, detail) = exception switch
    {
      ArgumentNullException => (
        StatusCodes.Status400BadRequest,
        "Solicitud invalida",
        UserFacingMessageTranslator.Translate(exception.Message)),
      ArgumentOutOfRangeException => (
        StatusCodes.Status400BadRequest,
        "Solicitud invalida",
        UserFacingMessageTranslator.Translate(exception.Message)),
      ArgumentException => (
        StatusCodes.Status400BadRequest,
        "Solicitud invalida",
        UserFacingMessageTranslator.Translate(exception.Message)),
      InvalidOperationException => (
        StatusCodes.Status400BadRequest,
        "No fue posible completar la operacion",
        UserFacingMessageTranslator.Translate(exception.Message)),
      KeyNotFoundException => (
        StatusCodes.Status404NotFound,
        "Recurso no encontrado",
        UserFacingMessageTranslator.Translate(exception.Message)),
      UnauthorizedAccessException => (
        StatusCodes.Status401Unauthorized,
        "No autorizado",
        UserFacingMessageTranslator.Translate(exception.Message)),
      _ => (
        StatusCodes.Status500InternalServerError,
        "Ocurrio un error inesperado",
        _environment.IsDevelopment() ? exception.Message : null)
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
      Detail = detail,
      Instance = context.Request.Path
    };

    problemDetails.Extensions["correlationId"] = correlationId;

    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";

    await context.Response.WriteAsJsonAsync(problemDetails);
  }
}
