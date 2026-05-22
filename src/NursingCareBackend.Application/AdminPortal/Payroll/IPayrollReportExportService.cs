namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IPayrollReportExportService
{
    byte[] GeneratePdf(AdminPayrollPeriodDetail period);

    byte[] GenerateXlsx(AdminPayrollPeriodDetail period);

    byte[] GenerateHtml(AdminPayrollPeriodDetail period);
}
