using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;

public sealed class AssignCareRequestNurseHandler
{
    private readonly ICareRequestRepository _careRequestRepository;
    private readonly IUserRepository _userRepository;

    public AssignCareRequestNurseHandler(
        ICareRequestRepository careRequestRepository,
        IUserRepository userRepository)
    {
        _careRequestRepository = careRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Domain.CareRequests.CareRequest> Handle(
        AssignCareRequestNurseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AssignedNurse == Guid.Empty)
        {
            throw new ArgumentException("Assigned nurse is required.", nameof(command.AssignedNurse));
        }

        var careRequest = await _careRequestRepository.GetByIdAsync(
            command.CareRequestId,
            CareRequestAccessScope.Admin,
            cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        var nurseUser = await _userRepository.GetByIdAsync(command.AssignedNurse, cancellationToken);
        if (nurseUser is null)
        {
            throw new InvalidOperationException("Assigned nurse was not found.");
        }

        if (nurseUser.ProfileType != UserProfileType.Nurse || nurseUser.NurseProfile is null)
        {
            throw new InvalidOperationException("Assigned user is not a nurse profile.");
        }

        if (!nurseUser.IsActive || !nurseUser.NurseProfile.IsActive)
        {
            throw new InvalidOperationException("Assigned nurse must have a completed active profile.");
        }

        careRequest.AssignNurse(command.AssignedNurse, DateTime.UtcNow);
        await _careRequestRepository.UpdateAsync(careRequest, cancellationToken);

        return careRequest;
    }
}
