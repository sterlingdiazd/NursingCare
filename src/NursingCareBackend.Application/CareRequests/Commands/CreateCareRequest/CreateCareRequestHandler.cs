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

        var careRequest = CareRequest.Create(new CareRequestCreateParams
        {
            UserID = command.UserID,
            Description = command.Description,
            CareRequestReason = command.CareRequestReason,
            CareRequestType = command.CareRequestType,
            UnitType = pricing.UnitType,
            SuggestedNurse = command.SuggestedNurse,
            AssignedNurse = command.AssignedNurse,
            Unit = command.Unit,
            Price = pricing.Price,
            Total = pricing.Total,
            ClientBasePrice = command.ClientBasePriceOverride,
            DistanceFactor = pricing.DistanceFactorCode,
            ComplexityLevel = pricing.ComplexityLevelCode,
            MedicalSuppliesCost = command.MedicalSuppliesCost,
            CareRequestDate = command.CareRequestDate,
            PricingCategoryCode = pricing.PricingCategoryCode,
            CategoryFactorSnapshot = pricing.CategoryFactorSnapshot,
            DistanceFactorMultiplierSnapshot = pricing.DistanceFactorMultiplierSnapshot,
            ComplexityMultiplierSnapshot = pricing.ComplexityMultiplierSnapshot,
            VolumeDiscountPercentSnapshot = pricing.VolumeDiscountPercentSnapshot,
            LineBeforeVolumeDiscount = pricing.LineBeforeVolumeDiscount,
            UnitPriceAfterVolumeDiscount = pricing.UnitPriceAfterVolumeDiscount,
            SubtotalBeforeSupplies = pricing.SubtotalBeforeSupplies,
            CreatedAtUtc = DateTime.UtcNow,
        });

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
