using System;

namespace NursingCareBackend.Domain.SystemSettings;

public sealed class SystemSetting
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public required string ValueType { get; set; } // "String", "Number", "Boolean", "Select"
    public string? AllowedValuesJson { get; set; } // For "Select" type
    public DateTime ModifiedAtUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
