using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.AdminPortal.Shifts;

public sealed class RecordCareRequestShiftChangeHandler
{
  private readonly IShiftRecordAdminRepository _shiftRepository;
  private readonly IUserRepository _userRepository;

  public RecordCareRequestShiftChangeHandler(
    IShiftRecordAdminRepository shiftRepository,
    IUserRepository userRepository)
  {
    _shiftRepository = shiftRepository;
    _userRepository = userRepository;
  }

  public async Task Handle(RecordCareRequestShiftChangeCommand command, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(command.Reason))
    {
      throw new ArgumentException("Reason is required.", nameof(command.Reason));
    }

    if (command.NewNurseUserId == Guid.Empty)
    {
      throw new ArgumentException("New nurse user id cannot be empty when provided.", nameof(command.NewNurseUserId));
    }

    if (command.NewNurseUserId is { } newNurse && newNurse != Guid.Empty)
    {
      await EnsureActiveNurseAsync(newNurse, cancellationToken);
    }

    var effectiveAt = command.EffectiveAtUtc ?? DateTime.UtcNow;

    await _shiftRepository.RecordShiftChangeAsync(
      command.CareRequestId,
      command.ShiftRecordId,
      command.NewNurseUserId,
      command.Reason.Trim(),
      effectiveAt,
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
