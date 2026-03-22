namespace NursingCareBackend.Application.Catalogs;

public interface IPricingPreviewService
{
    Task<PricingPreviewResponse> PreviewAsync(PricingPreviewRequest request, CancellationToken cancellationToken);
}
