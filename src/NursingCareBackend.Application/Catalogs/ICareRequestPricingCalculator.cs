using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

namespace NursingCareBackend.Application.Catalogs;

public interface ICareRequestPricingCalculator
{
    Task<string> ResolveUnitTypeAsync(string careRequestTypeCode, CancellationToken cancellationToken);

    Task<CareRequestPricingResult> CalculateAsync(
        CreateCareRequestCommand command,
        int existingSameUnitTypeCount,
        CancellationToken cancellationToken);
}
