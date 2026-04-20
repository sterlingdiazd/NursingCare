namespace NursingCareBackend.Domain.CareRequests;

public enum CareRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Completed = 3,
    Cancelled = 4,
    Invoiced = 5,
    Paid = 6,
    Voided = 7
}
