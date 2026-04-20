using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.AdminPortal.Clients;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Shifts;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;
using NursingCareBackend.Application.CareRequests.Commands.InvoiceCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.PayCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.VoidCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;
using NursingCareBackend.Application.CareRequests.Queries.GetReceipt;

namespace NursingCareBackend.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplicationServices(this IServiceCollection services)
  {
    services.AddScoped<AssignCareRequestNurseHandler>();
    services.AddScoped<CreateCareRequestHandler>();
    services.AddScoped<TransitionCareRequestHandler>();
    services.AddScoped<GetCareRequestsHandler>();
    services.AddScoped<GetCareRequestByIdHandler>();
    services.AddScoped<VerifyPricingHandler>();
    services.AddScoped<GetAdminDashboardHandler>();
    services.AddScoped<GetAdminActionQueueHandler>();
    services.AddScoped<GetAdminCareRequestsHandler>();
    services.AddScoped<GetAdminCareRequestDetailHandler>();
    services.AddScoped<GetAdminCareRequestClientOptionsHandler>();
    services.AddScoped<RegisterCareRequestShiftHandler>();
    services.AddScoped<RecordCareRequestShiftChangeHandler>();
    services.AddScoped<GetAdminClientsHandler>();
    services.AddScoped<GetAdminClientDetailHandler>();
    services.AddScoped<GetAdminUsersHandler>();
    services.AddScoped<GetAdminUserDetailHandler>();
    services.AddScoped<InvoiceCareRequestHandler>();
    services.AddScoped<PayCareRequestHandler>();
    services.AddScoped<VoidCareRequestHandler>();
    services.AddScoped<GenerateReceiptHandler>();
    services.AddScoped<GetReceiptHandler>();
    services.AddScoped<IAdminAccountProvisioningService, AdminAccountProvisioningService>();
    services.AddScoped<IAdminClientManagementService, AdminClientManagementService>();
    services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();

    return services;
  }
}
