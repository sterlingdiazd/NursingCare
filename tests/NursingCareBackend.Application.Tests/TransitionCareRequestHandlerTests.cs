using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.CareRequests.Commands.TransitionCareRequest;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.Tests;

public sealed class TransitionCareRequestHandlerTests
{
  [Fact]
  public async Task Handle_Should_Approve_And_Persist_Pending_Request()
  {
    var careRequest = CareRequest.Create(Guid.NewGuid(), "Approve me");
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
    var careRequest = CareRequest.Create(Guid.NewGuid(), "Reject me");
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
    var careRequest = CareRequest.Create(Guid.NewGuid(), "Complete me");
    careRequest.Approve(DateTime.UtcNow.AddMinutes(-5));

    var repository = new FakeCareRequestRepository(careRequest);
    var handler = new TransitionCareRequestHandler(repository);

    var result = await handler.Handle(
      new TransitionCareRequestCommand(careRequest.Id, CareRequestTransitionAction.Complete),
      CancellationToken.None);

    Assert.Equal(CareRequestStatus.Completed, result.Status);
    Assert.NotNull(result.CompletedAtUtc);
    Assert.Same(careRequest, repository.UpdatedCareRequest);
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

    public Task<IReadOnlyList<CareRequest>> GetAllAsync(CancellationToken cancellationToken)
      => Task.FromResult<IReadOnlyList<CareRequest>>(_items.Values.ToList());

    public Task<CareRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
      => Task.FromResult(_items.TryGetValue(id, out var careRequest) ? careRequest : null);

    public Task UpdateAsync(CareRequest careRequest, CancellationToken cancellationToken)
    {
      UpdatedCareRequest = careRequest;
      _items[careRequest.Id] = careRequest;
      return Task.CompletedTask;
    }
  }
}
