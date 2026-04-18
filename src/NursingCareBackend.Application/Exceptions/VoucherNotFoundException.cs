namespace NursingCareBackend.Application.Exceptions;

/// <summary>
/// Thrown when payroll voucher data cannot be found for a given period and/or nurse.
/// Internal identifiers are stored for logging purposes only and must not be exposed in user-facing responses.
/// </summary>
public sealed class VoucherNotFoundException : Exception
{
    /// <summary>
    /// The payroll period identifier. For internal logging only.
    /// </summary>
    public Guid? PeriodId { get; }

    /// <summary>
    /// The nurse user identifier. For internal logging only.
    /// </summary>
    public Guid? NurseId { get; }

    public VoucherNotFoundException(Guid periodId, Guid nurseId)
        : base("Voucher not found for the specified period and nurse.")
    {
        PeriodId = periodId;
        NurseId = nurseId;
    }

    public VoucherNotFoundException(Guid periodId)
        : base("Voucher not found for the specified period.")
    {
        PeriodId = periodId;
        NurseId = null;
    }
}
