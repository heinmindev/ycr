using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A service's human-facing code.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ43) — not a Myanma
/// Railways answer: a service code follows the station and route rules, 2-10 characters of
/// <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c> (<see cref="CodeFormat"/>). It is <em>not</em> unique:
/// two services may share a code when their effective periods do not overlap (R35), which the
/// create and withdraw handlers enforce under the code lock. If Myanma Railways later gives an
/// official, different answer to OQ43, that supersedes this ruling and needs its own follow-up task.
/// </remarks>
public sealed record ServiceCode
{
    private ServiceCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<ServiceCode> Create(string? value)
    {
        var trimmed = value?.Trim();
        return CodeFormat.IsValid(trimmed)
            ? new ServiceCode(trimmed!)
            : TimetableErrors.InvalidServiceCode;
    }

    internal static ServiceCode From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ServiceCode(value);
    }

    public override string ToString() => Value;
}
