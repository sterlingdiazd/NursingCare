namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IPayrollReportExportService
{
    byte[] GeneratePdf(AdminPayrollPeriodDetail period, CompanyInfo company);

    byte[] GenerateXlsx(AdminPayrollPeriodDetail period, CompanyInfo company);

    byte[] GenerateHtml(AdminPayrollPeriodDetail period, CompanyInfo company);
}
