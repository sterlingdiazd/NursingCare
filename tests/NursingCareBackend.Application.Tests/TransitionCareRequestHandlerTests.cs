using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Application.Payroll;

namespace NursingCareBackend.Application.Tests;

public sealed class TransitionCareRequestHandlerTests
{
  private static readonly Guid AssignedNurseId = Guid.Parse("11111111-1111-1111-1111-111111111111");

  private static CareRequest CreateDomicilioSample(
    Guid userId,
    string description,
    DateOnly? careRequestDate = null)
  {
    return CareRequest.Create(
      userID: userId,
      description: description,
      careRequestReason: null,
      careRequestType: "domicilio_24h",
      unitType: "dia_completo",
      suggestedNurse: null,
      assignedNurse: AssignedNurseId,
      unit: 1,
      price: 3500m,
      total: 4200m,
      clientBasePrice: null,
      distanceFactor: "local",
      complexityLevel: "estandar",
      medicalSuppliesCost: null,
      careRequestDate: careRequestDate,
      pricingCategoryCode: "domicilio",
      categoryFactorSnapshot: 1.2m,
      distanceFactorMultiplierSnapshot: 1.0m,
      complexityMultiplierSnapshot: 1.0m,
      volumeDiscountPercentSnapshot: 0,
      createdAtUtc: DateTime.UtcNow);
  }

  [Fact]
  public async Task Handle_Should_Approve_And_Persist_Pending_Request()
  {
    var careRequest = CreateDomicilioSample(Guid.NewGuid(), "Approve me");
    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository, new FakeAdminNotificationPublisher(), new FakePayrollCompensationService());

    var result = await handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Approve),
      CancellationToken.None);

    Assert.Equal(CareRequestStatus.Approved, result.Status);
    Assert.NotNull(result.ApprovedAtUtc);
    Assert.Same(careRequest, repository.UpdatedCareRequest);
  }

  [Fact]
  public async Task Handle_Should_Reject_And_Persist_Pending_Request()
  {
    var careRequest = CreateDomicilioSample(Guid.NewGuid(), "Reject me");
    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository, new FakeAdminNotificationPublisher(), new FakePayrollCompensationService());

    var result = await handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Reject),
      CancellationToken.None);

    Assert.Equal(CareRequestStatus.Rejected, result.Status);
    Assert.NotNull(result.RejectedAtUtc);
    Assert.Same(careRequest, repository.UpdatedCareRequest);
  }

  [Fact]
  public async Task Handle_Should_Complete_And_Persist_Approved_Request()
  {
    var careRequest = CreateDomicilioSample(Guid.NewGuid(), "Complete me");
    careRequest.Approve(DateTime.UtcNow.AddMinutes(-5));

    var repository = new FakeCareRequestRepository(careRequest);
    var payrollService = new FakePayrollCompensationService();
    var handler = new TransitionCareRequestHandler(repository, new FakeAdminNotificationPublisher(), payrollService);

    var result = await handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Complete, AssignedNurseId),
      CancellationToken.None);

    Assert.Equal(CareRequestStatus.Completed, result.Status);
    Assert.NotNull(result.CompletedAtUtc);
    Assert.Same(careRequest, repository.UpdatedCareRequest);
    Assert.Equal(careRequest.Id, payrollService.LastRecordedCareRequestId);
  }

  [Fact]
  public async Task Handle_Should_Throw_When_Completion_Date_Is_In_The_Future()
  {
    var careRequest = CreateDomicilioSample(
      Guid.NewGuid(),
      "Future completion",
      DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
    careRequest.Approve(DateTime.UtcNow.AddMinutes(-5));

    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository, new FakeAdminNotificationPublisher(), new FakePayrollCompensationService());

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Complete, AssignedNurseId),
      CancellationToken.None));

    Assert.Contains("cannot be completed before its scheduled care-request date", exception.Message);
  }

  [Fact]
  public async Task Handle_Should_Throw_When_Request_Does_Not_Exist()
  {
    var repository = new FakeCareRequestRepository();
    var handler = new TransitionCareRequestHandler(repository, new FakeAdminNotificationPublisher(), new FakePayrollCompensationService());

    var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
      new TransitionCareRequestCommand(Guid.NewGuid(), CareRequestTransitionAction.Approve),
      CancellationToken.None));

    Assert.Contains("was not found", exception.Message);
  }

  private sealed class FakeCareRequestRepository : ICareRequestRepository
  {
    private readonly Dictionary<Guid, CareRequest> _items = new();

    public FakeCareRequestRepository(params CareRequest[] careRequests)
    {
      foreach (var careRequest in careRequests)
      {
        _items[careRequest.Id] = careRequest;
      }
    }

    public CareRequest? UpdatedCareRequest { get; private set; }

    public Task AddAsync(CareRequest careRequest, CancellationToken cancellationToken)
    {
      _items[careRequest.Id] = careRequest;
      return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CareRequest>> GetAllAsync(CareRequestAccessScope scope, CancellationToken cancellationToken)
    {
      var items = _items.Values
        .Where(careRequest =>
          scope.CreatedByUserId is null || careRequest.UserID == scope.CreatedByUserId.Value)
        .Where(careRequest =>
          scope.AssignedNurseUserId is null || careRequest.AssignedNurse == scope.AssignedNurseUserId.Value)
        .ToList();

      return Task.FromResult<IReadOnlyList<CareRequest>>(items);
    }

    public Task<CareRequest?> GetByIdAsync(Guid id, CareRequestAccessScope scope, CancellationToken cancellationToken)
    {
      if (!_items.TryGetValue(id, out var careRequest))
      {
        return Task.FromResult<CareRequest?>(null);
      }

      if (scope.CreatedByUserId is not null && careRequest.UserID != scope.CreatedByUserId.Value)
      {
        return Task.FromResult<CareRequest?>(null);
      }

      if (scope.AssignedNurseUserId is not null && careRequest.AssignedNurse != scope.AssignedNurseUserId.Value)
      {
        return Task.FromResult<CareRequest?>(null);
      }

      return Task.FromResult<CareRequest?>(careRequest);
    }

    public Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken)
    {
      UpdatedCareRequest = careRequest;
      _items[careRequest.Id] = careRequest;
      return Task.CompletedTask;
    }

    public Task<int> CountByUserAndUnitTypeAsync(
      Guid clientId,
      string unitType,
      CancellationToken cancellationToken)
      => Task.FromResult(0);
  }

  private sealed class FakePayrollCompensationService : IPayrollCompensationService
  {
    public Guid? LastRecordedCareRequestId { get; private set; }

    public Task RecordExecutionForCompletedCareRequestAsync(CareRequest careRequest, CancellationToken cancellationToken)
    {
      LastRecordedCareRequestId = careRequest.Id;
      return Task.CompletedTask;
    }
  }
}
