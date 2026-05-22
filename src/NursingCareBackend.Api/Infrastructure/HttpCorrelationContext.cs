using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.Abstractions;

namespace NursingCareBackend.Api.Infrastructure;

/// <summary>
/// HTTP adapter for <see cref="ICorrelationContext"/>: exposes the per-request correlation id
/// (set by CorrelationIdMiddleware) to inner layers (e.g., the audit service).
/// </summary>
public sealed class HttpCorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCorrelationContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? CorrelationId => _accessor.HttpContext?.GetCorrelationId();
}
