using System.Text.RegularExpressions;
using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A station's human-facing code.
/// </summary>
/// <remarks>
/// DECISION (tech lead, hein, 2026-09-22; T-014) - final; not a Myanma Railways answer:
/// a station code is 2-10 characters and contains only A-Z and 0-9. If Myanma Railways
/// later gives an official, different answer to OQ26, that supersedes this ruling and
/// needs its own follow-up task.
/// </remarks>
public sealed record StationCode
{
    private static readonly Regex ValidPattern = new(
        "^[A-Z0-9]{2,10}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private StationCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<StationCode> Create(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is not null && ValidPattern.IsMatch(trimmed)
            ? new StationCode(trimmed)
            : NetworkErrors.InvalidStationCode;
    }

    internal static StationCode From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StationCode(value);
    }

    public override string ToString() => Value;
}
