namespace NursingCareBackend.Application.CareRequests;

public sealed record CareRequestAccessScope(
    Guid? CreatedByUserId,
    Guid? AssignedNurseUserId)
{
    public static readonly CareRequestAccessScope Admin = new(null, null);

    public static CareRequestAccessScope ForClient(Guid userId) => new(userId, null);

    public static CareRequestAccessScope ForNurse(Guid userId) => new(null, userId);
}
