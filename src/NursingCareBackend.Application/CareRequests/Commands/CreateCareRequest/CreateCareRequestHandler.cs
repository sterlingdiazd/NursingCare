using NursingCareBackend.Application.Catalogs;
using NursingCareBackend.Application.AdminPortal.Notifications;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;

public sealed class CreateCareRequestHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly ICareRequestPricingCalculator _pricingCalculator;
    private readonly IAdminNotificationPublisher _notifications;

    public CreateCareRequestHandler(
        ICareRequestRepository repository,
        ICareRequestPricingCalculator pricingCalculator,
        IAdminNotificationPublisher notifications)
    {
        _repository = repository;
        _pricingCalculator = pricingCalculator;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(CreateCareRequestCommand command, CancellationToken cancellationToken)
    {
        var unitType = await _pricingCalculator.ResolveUnitTypeAsync(command.CareRequestType, cancellationToken);
        var existingSameUnitTypeCount = await _repository.CountByUserAndUnitTypeAsync(
            command.UserID,
            unitType,
            cancellationToken);

        var pricing = await _pricingCalculator.CalculateAsync(command, existingSameUnitTypeCount, cancellationToken);

        var careRequest = CareRequest.Create(
            userID: command.UserID,
            description: command.Description,
            careRequestReason: command.CareRequestReason,
            careRequestType: command.CareRequestType,
            unitType: pricing.UnitType,
            suggestedNurse: command.SuggestedNurse,
            assignedNurse: command.AssignedNurse,
            unit: command.Unit,
            price: pricing.Price,
            total: pricing.Total,
            clientBasePrice: command.ClientBasePriceOverride,
            distanceFactor: pricing.DistanceFactorCode,
            complexityLevel: pricing.ComplexityLevelCode,
            medicalSuppliesCost: command.MedicalSuppliesCost,
            careRequestDate: command.CareRequestDate,
            pricingCategoryCode: pricing.PricingCategoryCode,
            categoryFactorSnapshot: pricing.CategoryFactorSnapshot,
            distanceFactorMultiplierSnapshot: pricing.DistanceFactorMultiplierSnapshot,
            complexityMultiplierSnapshot: pricing.ComplexityMultiplierSnapshot,
            volumeDiscountPercentSnapshot: pricing.VolumeDiscountPercentSnapshot,
            createdAtUtc: DateTime.UtcNow);

        await _repository.AddAsync(careRequest, cancellationToken);

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "care_request_created",
                Severity: "Medium",
                Title: "Nueva solicitud de cuidado creada",
                Body: $"La solicitud \"{careRequest.Description}\" fue creada y requiere seguimiento administrativo.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Sistema",
                RequiresAction: true),
            cancellationToken);

        await _notifications.PublishToAdminsAsync(
            new AdminNotificationPublishRequest(
                Category: "care_request_pending_assignment",
                Severity: "High",
                Title: "Solicitud pendiente de asignacion",
                Body: $"La solicitud \"{careRequest.Description}\" requiere asignar una enfermera activa.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/admin/care-requests/{careRequest.Id}",
                Source: "Sistema",
                RequiresAction: true),
            cancellationToken);

        return careRequest.Id;
    }
}
