namespace NursingCareBackend.Application.Abstractions;

/// <summary>
/// Exposes the current request's correlation id to inner layers without depending on HTTP.
/// The Api layer provides the adapter (reads it from the HttpContext set by CorrelationIdMiddleware).
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
