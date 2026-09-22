using System.Text.RegularExpressions;
using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A station's human-facing code.
/// </summary>
/// <remarks>
/// ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered):
/// a station code is 2-10 characters and contains only A-Z and 0-9.
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
