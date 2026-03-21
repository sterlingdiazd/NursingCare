using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Application.CareRequests;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.Tests;

public sealed class TransitionCareRequestHandlerTests
{
  private static readonly Guid AssignedNurseId = Guid.Parse("11111111-1111-1111-1111-111111111111");

  [Fact]
  public async Task Handle_Should_Approve_And_Persist_Pending_Request()
  {
    var careRequest = CareRequest.Create(
      userID: Guid.NewGuid(),
      description: "Approve me",
      careRequestReason: null,
      careRequestType: "domicilio_24h",
      suggestedNurse: null,
      assignedNurse: AssignedNurseId,
      unit: 1,
      price: null,
      clientBasePrice: null,
      distanceFactor: null,
      complexityLevel: null,
      medicalSuppliesCost: null,
      careRequestDate: null,
      existingSameUnitTypeCount: 0);
    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository);

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
    var careRequest = CareRequest.Create(
      userID: Guid.NewGuid(),
      description: "Reject me",
      careRequestReason: null,
      careRequestType: "domicilio_24h",
      suggestedNurse: null,
      assignedNurse: AssignedNurseId,
      unit: 1,
      price: null,
      clientBasePrice: null,
      distanceFactor: null,
      complexityLevel: null,
      medicalSuppliesCost: null,
      careRequestDate: null,
      existingSameUnitTypeCount: 0);
    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository);

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
    var careRequest = CareRequest.Create(
      userID: Guid.NewGuid(),
      description: "Complete me",
      careRequestReason: null,
      careRequestType: "domicilio_24h",
      suggestedNurse: null,
      assignedNurse: AssignedNurseId,
      unit: 1,
      price: null,
      clientBasePrice: null,
      distanceFactor: null,
      complexityLevel: null,
      medicalSuppliesCost: null,
      careRequestDate: null,
      existingSameUnitTypeCount: 0);
    careRequest.Approve(DateTime.UtcNow.AddMinutes(-5));

    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository);

    var result = await handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Complete, AssignedNurseId),
      CancellationToken.None);

    Assert.Equal(CareRequestStatus.Completed, result.Status);
    Assert.NotNull(result.CompletedAtUtc);
    Assert.Same(careRequest, repository.UpdatedCareRequest);
  }

  [Fact]
  public async Task Handle_Should_Throw_When_Completion_Date_Is_In_The_Future()
  {
    var careRequest = CareRequest.Create(
      userID: Guid.NewGuid(),
      description: "Future completion",
      careRequestReason: null,
      careRequestType: "domicilio_24h",
      suggestedNurse: null,
      assignedNurse: AssignedNurseId,
      unit: 1,
      price: null,
      clientBasePrice: null,
      distanceFactor: null,
      complexityLevel: null,
      medicalSuppliesCost: null,
      careRequestDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
      existingSameUnitTypeCount: 0);
    careRequest.Approve(DateTime.UtcNow.AddMinutes(-5));

    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository);

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Complete, AssignedNurseId),
      CancellationToken.None));

    Assert.Contains("cannot be completed before its scheduled care-request date", exception.Message);
  }

  [Fact]
  public async Task Handle_Should_Throw_When_Request_Does_Not_Exist()
  {
    var repository = new FakeCareRequestRepository();
    var handler = new TransitionCareRequestHandler(repository);

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
}
