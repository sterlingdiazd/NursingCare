using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

public sealed class CreateCareRequestHandler
{
    private readonly ICareRequestRepository _repository;

    public CreateCareRequestHandler(ICareRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateCareRequestCommand command, CancellationToken cancellationToken)
    {
        var unitType = CareRequest.GetUnitTypeForCareRequestType(command.CareRequestType);
        var existingSameUnitTypeCount = await _repository.CountByUserAndUnitTypeAsync(
            command.UserID,
            unitType,
            cancellationToken);

        var careRequest = CareRequest.Create(
            userID: command.UserID,
            description: command.Description,
            careRequestReason: command.CareRequestReason,
            careRequestType: command.CareRequestType,
            suggestedNurse: command.SuggestedNurse,
            assignedNurse: command.AssignedNurse,
            unit: command.Unit,
            price: command.Price,
            clientBasePrice: command.ClientBasePriceOverride,
            distanceFactor: command.DistanceFactor,
            complexityLevel: command.ComplexityLevel,
            medicalSuppliesCost: command.MedicalSuppliesCost,
            careRequestDate: command.CareRequestDate,
            existingSameUnitTypeCount: existingSameUnitTypeCount);

        await _repository.AddAsync(careRequest, cancellationToken);

        return careRequest.Id;
    }
}
