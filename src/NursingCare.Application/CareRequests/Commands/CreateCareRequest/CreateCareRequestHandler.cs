using NursingCare.Domain.CareRequests;

namespace NursingCare.Application.CareRequests.Commands.CreateCareRequest;

public sealed class CreateCareRequestHandler
{
    private readonly ICareRequestRepository _repository;

    public CreateCareRequestHandler(ICareRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateCareRequestCommand command, CancellationToken cancellationToken)
    {
        var careRequest = CareRequest.Create(
            command.ResidentId,
            command.Description
        );

        await _repository.AddAsync(careRequest, cancellationToken);

        return careRequest.Id;
    }
}
