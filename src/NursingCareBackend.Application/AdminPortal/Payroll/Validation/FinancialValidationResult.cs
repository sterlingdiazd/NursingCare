namespace NursingCareBackend.Application.AdminPortal.Payroll.Validation;

/// <summary>
/// Outcome of a financial-output validation. <see cref="IsValid"/> is true only when there are
/// no failures. On failure, <see cref="Failures"/> lists each broken rule and
/// <see cref="ReasonSummary"/> is a single Spanish sentence suitable for showing to an admin
/// (e.g. as the blocked-delivery reason or a problem-details detail).
/// </summary>
public sealed class FinancialValidationResult
{
    private FinancialValidationResult(
        bool isValid,
        IReadOnlyList<FinancialValidationFailure> failures,
        string reasonSummary)
    {
        IsValid = isValid;
        Failures = failures;
        ReasonSummary = reasonSummary;
    }

    public bool IsValid { get; }

    public IReadOnlyList<FinancialValidationFailure> Failures { get; }

    /// <summary>A short Spanish summary of the outcome (success note or the blocking reason).</summary>
    public string ReasonSummary { get; }

    public static FinancialValidationResult Success(string note = "El documento financiero superó la validación.") =>
        new(true, Array.Empty<FinancialValidationFailure>(), note);

    public static FinancialValidationResult Failure(IReadOnlyList<FinancialValidationFailure> failures)
    {
        if (failures.Count == 0)
        {
            // Defensive: a "failure" with no entries would be indistinguishable from success.
            throw new ArgumentException("A failed validation result must carry at least one failure.", nameof(failures));
        }

        var summary = "El comprobante no superó la validación financiera: "
            + string.Join(" ", failures.Select(f => f.Message));
        return new FinancialValidationResult(false, failures, summary);
    }
}

/// <summary>A single broken validation rule: a stable machine code plus a Spanish, user-facing message.</summary>
public sealed record FinancialValidationFailure(string Code, string Message);
