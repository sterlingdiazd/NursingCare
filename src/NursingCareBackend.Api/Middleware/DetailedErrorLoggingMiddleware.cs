using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace NursingCareBackend.Api.Middleware;

/// <summary>
/// Middleware that logs and provides detailed error information for all HTTP error responses
/// to help with debugging issues across all endpoints.
/// </summary>
public sealed class DetailedErrorLoggingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<DetailedErrorLoggingMiddleware> _logger;
  private readonly IHostEnvironment _environment;

  public DetailedErrorLoggingMiddleware(
    RequestDelegate next,
    ILogger<DetailedErrorLoggingMiddleware> logger,
    IHostEnvironment environment)
  {
    _next = next;
    _logger = logger;
    _environment = environment;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    // Capture the original response body stream
    var originalBodyStream = context.Response.Body;

    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    try
    {
      await _next(context);

      // Check if we need to enhance the error response
      if (context.Response.StatusCode >= 400)
      {
        await HandleErrorResponseAsync(context, originalBodyStream, responseBody);
      }
      else
      {
        // Copy the response back to the original stream
        await CopyResponseAsync(responseBody, originalBodyStream);
      }
    }
    finally
    {
      context.Response.Body = originalBodyStream;
    }
  }

  private async Task HandleErrorResponseAsync(HttpContext context, Stream originalBodyStream, MemoryStream responseBody)
  {
    responseBody.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(responseBody).ReadToEndAsync();
    responseBody.Seek(0, SeekOrigin.Begin);

    var statusCode = context.Response.StatusCode;
    var method = context.Request.Method;
    var path = context.Request.Path;
    var queryString = context.Request.QueryString.ToString();

    // Log the error with context
    LogErrorWithContext(context, statusCode, method, path, queryString, responseText);

    // Check if we need to enhance the response
    var needsEnhancement = string.IsNullOrWhiteSpace(responseText) || 
                          responseText.Length < 50 ||
                          !responseText.Contains("\"title\"") ||
                          !responseText.Contains("\"detail\"");

    if (needsEnhancement)
    {
      // Create enhanced error response
      var problemDetails = CreateEnhancedProblemDetails(context, statusCode, responseText);
      
      context.Response.ContentType = "application/problem+json";
      context.Response.Body = originalBodyStream;
      await context.Response.WriteAsJsonAsync(problemDetails);
    }
    else
    {
      // Copy the existing response
      await CopyResponseAsync(responseBody, originalBodyStream);
    }
  }

  private void LogErrorWithContext(HttpContext context, int statusCode, string method, string path, string queryString, string responseText)
  {
    var user = context.User;
    var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
    var userName = user?.Identity?.Name ?? "Anonymous";
    var userId = user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "None";
    var roles = string.Join(", ", user?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>());
    var hasAuthHeader = context.Request.Headers.ContainsKey("Authorization");

    var logMessage = new StringBuilder();
    logMessage.AppendLine($"HTTP {statusCode} Error:");
    logMessage.AppendLine($"  Endpoint: {method} {path}{queryString}");
    logMessage.AppendLine($"  User: {userName} (ID: {userId})");
    logMessage.AppendLine($"  Authenticated: {isAuthenticated}");
    logMessage.AppendLine($"  Has Auth Header: {hasAuthHeader}");
    logMessage.AppendLine($"  Roles: {(string.IsNullOrEmpty(roles) ? "None" : roles)}");
    
    if (!string.IsNullOrWhiteSpace(responseText) && responseText.Length < 500)
    {
      logMessage.AppendLine($"  Response: {responseText}");
    }

    if (statusCode >= 500)
    {
      _logger.LogError(logMessage.ToString());
    }
    else if (statusCode == 401 || statusCode == 403)
    {
      _logger.LogWarning(logMessage.ToString());
    }
    else
    {
      _logger.LogInformation(logMessage.ToString());
    }
  }

  private ProblemDetails CreateEnhancedProblemDetails(HttpContext context, int statusCode, string existingResponse)
  {
    var user = context.User;
    var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
    var hasAuthHeader = context.Request.Headers.ContainsKey("Authorization");
    var roles = user?.Claims
      .Where(c => c.Type == ClaimTypes.Role)
      .Select(c => c.Value)
      .ToList() ?? new List<string>();

    var (title, detail) = statusCode switch
    {
      400 => ("Solicitud incorrecta", GetBadRequestDetail(context, existingResponse)),
      401 => ("No autenticado", GetUnauthorizedDetail(hasAuthHeader, isAuthenticated)),
      403 => ("Acceso denegado", GetForbiddenDetail(isAuthenticated, roles)),
      404 => ("Recurso no encontrado", $"El recurso solicitado '{context.Request.Path}' no existe."),
      405 => ("Método no permitido", $"El método {context.Request.Method} no está permitido para este endpoint."),
      409 => ("Conflicto", "La solicitud no se pudo completar debido a un conflicto con el estado actual del recurso."),
      422 => ("Entidad no procesable", "Los datos proporcionados no son válidos o están incompletos."),
      429 => ("Demasiadas solicitudes", "Ha excedido el límite de solicitudes. Por favor, intente más tarde."),
      500 => ("Error interno del servidor", "Ocurrió un error inesperado. El equipo técnico ha sido notificado."),
      502 => ("Puerta de enlace incorrecta", "Error de comunicación con el servidor. Por favor, intente más tarde."),
      503 => ("Servicio no disponible", "El servicio está temporalmente no disponible. Por favor, intente más tarde."),
      _ => ("Error", $"Ocurrió un error al procesar la solicitud. Código de estado: {statusCode}")
    };

    var problemDetails = new ProblemDetails
    {
      Status = statusCode,
      Title = title,
      Detail = detail,
      Instance = context.Request.Path
    };

    // Add diagnostic information
    problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
    problemDetails.Extensions["method"] = context.Request.Method;
    problemDetails.Extensions["path"] = context.Request.Path.ToString();
    
    if (context.Request.QueryString.HasValue)
    {
      problemDetails.Extensions["queryString"] = context.Request.QueryString.ToString();
    }

    // Add auth-related info for auth errors
    if (statusCode == 401 || statusCode == 403)
    {
      problemDetails.Extensions["isAuthenticated"] = isAuthenticated;
      problemDetails.Extensions["hasAuthorizationHeader"] = hasAuthHeader;
      
      if (statusCode == 403)
      {
        problemDetails.Extensions["userRoles"] = roles;
        problemDetails.Extensions["requiredRole"] = "Admin";
      }
    }

    // Add development-only details
    if (_environment.IsDevelopment() && !string.IsNullOrWhiteSpace(existingResponse))
    {
      problemDetails.Extensions["originalResponse"] = existingResponse;
    }

    return problemDetails;
  }

  private static string GetBadRequestDetail(HttpContext context, string existingResponse)
  {
    // Try to extract meaningful information from the existing response
    if (!string.IsNullOrWhiteSpace(existingResponse))
    {
      // Check if it's a validation error
      if (existingResponse.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
          existingResponse.Contains("invalid", StringComparison.OrdinalIgnoreCase))
      {
        return "Los datos proporcionados no son válidos. Por favor, verifique los campos y vuelva a intentar.";
      }

      // Check if it's a model binding error
      if (existingResponse.Contains("required", StringComparison.OrdinalIgnoreCase))
      {
        return "Faltan campos requeridos en la solicitud. Por favor, verifique que todos los campos obligatorios estén presentes.";
      }
    }

    // Check query parameters
    if (context.Request.QueryString.HasValue)
    {
      return $"Los parámetros de consulta proporcionados no son válidos: {context.Request.QueryString}";
    }

    return "La solicitud no pudo ser procesada. Por favor, verifique los datos enviados y vuelva a intentar.";
  }

  private static string GetUnauthorizedDetail(bool hasAuthHeader, bool isAuthenticated)
  {
    if (!hasAuthHeader)
    {
      return "No se proporciono un token de autenticacion. Por favor, inicie sesion.";
    }

    if (!isAuthenticated)
    {
      return "El token de autenticacion es invalido o ha expirado. Por favor, inicie sesion nuevamente.";
    }

    return "No se pudo autenticar la solicitud.";
  }

  private static string GetForbiddenDetail(bool isAuthenticated, List<string> roles)
  {
    if (!isAuthenticated)
    {
      return "Debe iniciar sesion para acceder a este recurso.";
    }

    if (roles.Count == 0)
    {
      return "Su cuenta no tiene roles asignados. Contacte al administrador del sistema.";
    }

    return $"Su cuenta tiene los roles: {string.Join(", ", roles)}. Este recurso requiere el rol 'Admin'.";
  }

  private static async Task CopyResponseAsync(Stream source, Stream destination)
  {
    source.Seek(0, SeekOrigin.Begin);
    await source.CopyToAsync(destination);
  }
}
