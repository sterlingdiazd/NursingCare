
namespace NursingCareBackend.Infrastructure;

public sealed class ResolvedConnectionString
{
    public string Value { get; }

    public ResolvedConnectionString(string value)
    {
        Value = value;
    }
}