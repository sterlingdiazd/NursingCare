using NursingCareBackend.Application.AdminPortal.Auditing;
using NursingCareBackend.Application.CareRequests.Commands.CreateCareRequest;
using NursingCareBackend.Application.Notifications;
using NursingCareBackend.Domain.CareRequests;

namespace NursingCareBackend.Application.CareRequests.Commands.IssueCreditNote;

/// <summary>
/// Records a credit note / refund against a Paid care request. The request stays Paid (Void is
/// blocked after Paid by design); this writes an auditable in-books ledger entry so a reversed/
/// refunded payment is no longer invisible. The aggregate enforces the money invariants (must be
/// Paid; credits can never exceed the amount paid); the handler supplies the running already-credited
/// total it reads from the repository.
/// </summary>
public sealed class IssueCreditNoteHandler
{
    private readonly ICareRequestRepository _repository;
    private readonly ICreditNoteRepository _creditNotes;
    private readonly IAdminAuditService _auditService;
    private readonly IUserNotificationPublisher _userNotifications;

    public IssueCreditNoteHandler(
        ICareRequestRepository repository,
        ICreditNoteRepository creditNotes,
        IAdminAuditService auditService,
        IUserNotificationPublisher userNotifications)
    {
        _repository = repository;
        _creditNotes = creditNotes;
        _auditService = auditService;
        _userNotifications = userNotifications;
    }

    public async Task<IssueCreditNoteResponse> Handle(
        IssueCreditNoteCommand command,
        CancellationToken cancellationToken)
    {
        var careRequest = await _repository.GetByIdAsync(
            command.CareRequestId, CareRequestAccessScope.Admin, cancellationToken);

        if (careRequest is null)
        {
            throw new KeyNotFoundException($"Care request '{command.CareRequestId}' was not found.");
        }

        // Read the running credited total, then let the aggregate enforce the cap. This is a
        // read-then-write (TOCTOU): two concurrent credit notes could both read the same total and
        // both pass the cap. Accepted for this single-admin deployment (one writer); if this ever
        // becomes multi-writer, re-validate the cap inside a transaction or add a DB check constraint.
        var alreadyCredited = await _creditNotes.GetTotalCreditedAsync(
            command.CareRequestId, cancellationToken);

        // Domain enforces: request must be Paid, and alreadyCredited + amount <= Total.
        var creditNote = careRequest.IssueCreditNote(
            command.Amount,
            command.Reason,
            command.Reference,
            command.ActingAdminUserId,
            DateTime.UtcNow,
            alreadyCredited);

        await _creditNotes.AddAsync(creditNote, cancellationToken);

        var totalCredited = alreadyCredited + creditNote.Amount;
        var referenceNote = string.IsNullOrWhiteSpace(creditNote.Reference)
            ? string.Empty
            : $" Ref: {creditNote.Reference}.";

        await _auditService.WriteAsync(
            new AdminAuditRecord(
                ActorUserId: command.ActingAdminUserId,
                ActorRole: "Admin",
                Action: AdminAuditActions.IssueCreditNote,
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                Notes: $"Nota de crédito/reembolso de RD${creditNote.Amount:N2} " +
                       $"(factura {careRequest.InvoiceNumber}). Motivo: {creditNote.Reason}.{referenceNote} " +
                       $"Total acreditado: RD${totalCredited:N2} de RD${careRequest.Total:N2}.",
                MetadataJson: null),
            cancellationToken);

        await _userNotifications.PublishToUserAsync(
            new UserNotificationPublishRequest(
                RecipientUserId: careRequest.UserID,
                Category: "credit_note_issued",
                Severity: "Medium",
                Title: "Nota de crédito emitida",
                Body: $"Se registró una nota de crédito/reembolso de RD${creditNote.Amount:N2} " +
                      $"sobre tu solicitud \"{careRequest.Description}\". Motivo: {creditNote.Reason}.",
                EntityType: "CareRequest",
                EntityId: careRequest.Id.ToString(),
                DeepLinkPath: $"/care-requests/{careRequest.Id}",
                Source: "Cobros",
                RequiresAction: false),
            cancellationToken);

        return new IssueCreditNoteResponse(
            creditNote.Id,
            careRequest.Id,
            creditNote.Amount,
            creditNote.Reason,
            creditNote.Reference,
            creditNote.IssuedAtUtc,
            totalCredited);
    }
}
