using System.Threading;
using System.Threading.Tasks;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.Payroll;

public interface IPayrollCompensationService
{
    Task RecordExecutionForCompletedCareRequestAsync(CareRequest careRequest, CancellationToken cancellationToken);
}
