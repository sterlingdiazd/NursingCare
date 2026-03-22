using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.ErrorHandling;

namespace NursingCareBackend.Api.Extensions;

public static class ControllerExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var firstError = context.ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? error.Exception?.Message : error.ErrorMessage)
                    .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

                var problemDetails = context.HttpContext.CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "La solicitud contiene datos invalidos.",
                    firstError ?? "Revisa los datos enviados e intenta de nuevo.");

                var validationProblemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = problemDetails.Status,
                    Title = problemDetails.Title,
                    Detail = problemDetails.Detail,
                    Instance = problemDetails.Instance,
                };

                foreach (var extension in problemDetails.Extensions)
                {
                    validationProblemDetails.Extensions[extension.Key] = extension.Value;
                }

                return new BadRequestObjectResult(validationProblemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }

    public static ObjectResult ProblemResponse(
        this ControllerBase controller,
        int statusCode,
        string title,
        string? detail = null)
    {
        var problemDetails = controller.HttpContext.CreateProblemDetails(statusCode, title, detail);

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static ProblemDetails CreateProblemDetails(
        this HttpContext context,
        int statusCode,
        string title,
        string? detail = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail is null ? null : UserFacingMessageTranslator.Translate(detail),
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = context.GetCorrelationId();

        return problemDetails;
    }
}
