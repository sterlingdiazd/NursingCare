using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed class RegisterCareRequestShiftHandler
{
  private readonly IShiftRecordAdminRepository _shiftRepository;
  private readonly IUserRepository _userRepository;

  public RegisterCareRequestShiftHandler(
    IShiftRecordAdminRepository shiftRepository,
    IUserRepository userRepository)
  {
    _shiftRepository = shiftRepository;
    _userRepository = userRepository;
  }

  public async Task<Guid> Handle(RegisterCareRequestShiftCommand command, CancellationToken cancellationToken)
  {
    if (command.NurseUserId == Guid.Empty)
    {
      throw new ArgumentException("Nurse user id cannot be empty when provided.", nameof(command.NurseUserId));
    }

    if (command.NurseUserId.HasValue)
    {
      await EnsureActiveNurseAsync(command.NurseUserId.Value, cancellationToken);
    }

    return await _shiftRepository.RegisterShiftAsync(
      command.CareRequestId,
      command.NurseUserId,
      command.ScheduledStartUtc,
      command.ScheduledEndUtc,
      cancellationToken);
  }

  private async Task EnsureActiveNurseAsync(Guid nurseUserId, CancellationToken cancellationToken)
  {
    var nurseUser = await _userRepository.GetByIdAsync(nurseUserId, cancellationToken);
    if (nurseUser is null)
    {
      throw new InvalidOperationException("Nurse was not found.");
    }

    if (nurseUser.ProfileType != UserProfileType.NURSE || nurseUser.NurseProfile is null)
    {
      throw new InvalidOperationException("User is not a nurse profile.");
    }

    if (!nurseUser.IsActive || !nurseUser.NurseProfile.IsActive)
    {
      throw new InvalidOperationException("Nurse must have a completed active profile.");
    }
  }
}
