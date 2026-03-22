using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Application.AdminPortal.Queries;
using NursingCareBackend.Application.AdminPortal.Users;
using NursingCareBackend.Application.CareRequests.Commands.AssignCareRequestNurse;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests.Queries;

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
    services.AddScoped<GetAdminDashboardHandler>();
    services.AddScoped<GetAdminActionQueueHandler>();
    services.AddScoped<GetAdminCareRequestsHandler>();
    services.AddScoped<GetAdminCareRequestDetailHandler>();
    services.AddScoped<GetAdminCareRequestClientOptionsHandler>();
    services.AddScoped<GetAdminUsersHandler>();
    services.AddScoped<GetAdminUserDetailHandler>();
    services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();

    return services;
  }
}
