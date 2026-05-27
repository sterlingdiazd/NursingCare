namespace NursingCareBackend.Application.CareRequests.Commands.GenerateReceipt;

/// <summary>
/// Generates (idempotently) the client receipt for a Paid care request. Extracted so callers that
/// orchestrate it as a side effect — e.g. PayCareRequestHandler auto-generating the receipt on Pay —
/// can depend on the abstraction and be unit tested without the full PDF/company/user dependency graph.
/// </summary>
public interface IGenerateReceiptHandler
{
    Task<GenerateReceiptResponse> Handle(
        GenerateReceiptCommand command,
        CancellationToken cancellationToken);
}
