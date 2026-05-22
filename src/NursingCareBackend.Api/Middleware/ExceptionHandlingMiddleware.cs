using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Api.ErrorHandling;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Notifications;

namespace NursingCareBackend.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly IHostEnvironment _environment;
  private readonly ILogger<ExceptionHandlingMiddleware> _logger;
  private readonly IServiceScopeFactory _scopeFactory;

  public ExceptionHandlingMiddleware(
    RequestDelegate next,
    IHostEnvironment environment,
    ILogger<ExceptionHandlingMiddleware> logger,
    IServiceScopeFactory scopeFactory)
  {
    _next = next;
    _environment = environment;
    _logger = logger;
    _scopeFactory = scopeFactory;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await _next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
      // Client closed the connection mid-request (e.g. user navigated away).
      // Don't surface as 500 / log as ERR. Use 499 (client closed request).
      _logger.LogDebug(
        "Request aborted by client for {Method} {Path}",
        context.Request.Method,
        context.Request.Path);
      if (!context.Response.HasStarted)
      {
        context.Response.StatusCode = 499;
      }
    }
    catch (Microsoft.AspNetCore.Http.BadHttpRequestException ex)
    {
      // Kestrel surfaces malformed/truncated requests (e.g. client closed mid-body)
      // as BadHttpRequestException. Honor the carried status code (typically 4xx)
      // instead of laundering them as 500s.
      _logger.LogDebug(
        ex,
        "Bad HTTP request for {Method} {Path}: {Message}",
        context.Request.Method,
        context.Request.Path,
        ex.Message);
      if (!context.Response.HasStarted)
      {
        context.Response.StatusCode = ex.StatusCode == 0 ? StatusCodes.Status400BadRequest : ex.StatusCode;
      }
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

    if (statusCode == StatusCodes.Status500InternalServerError)
    {
      await PublishSystemErrorNotificationAsync(context, correlationId, cancellationToken: default);
    }

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

  private async Task PublishSystemErrorNotificationAsync(
    HttpContext context,
    string correlationId,
    CancellationToken cancellationToken)
  {
    try
    {
      using var scope = _scopeFactory.CreateScope();
      var notifications = scope.ServiceProvider.GetRequiredService<IAdminNotificationPublisher>();
      await notifications.PublishToAdminsAsync(
        new AdminNotificationPublishRequest(
          Category: "system_error",
          Severity: "High",
          Title: "Error de sistema de alta severidad",
          Body: $"Se detecto un error en {context.Request.Method} {context.Request.Path} (correlacion {correlationId}).",
          EntityType: "SystemIssue",
          EntityId: correlationId,
          DeepLinkPath: "/admin/alerts",
          Source: "Sistema",
          RequiresAction: true),
        cancellationToken);
    }
    catch (Exception notificationError)
    {
      _logger.LogWarning(notificationError, "Failed to publish admin system-error notification.");
    }
  }
}
