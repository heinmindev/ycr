using System.Globalization;
using System.Text.RegularExpressions;
using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// A time of day in a timetable: whole minutes after local midnight at the start of the service's
/// operating date (ADR-0027 items 1-3; F-005 plan P2).
/// </summary>
/// <remarks>
/// <para>
/// BUSINESS DECISIONS — provisional tech-lead rulings (hein, 2026-09-26; T-053, OQ52, OQ53) — not
/// Myanma Railways answers: times are whole minutes (R11), and no service runs past midnight in
/// Phase 1, so every time is <c>00:00</c>-<c>23:59</c> (R15). If Myanma Railways later answers
/// differently, that supersedes these rulings and needs its own follow-up task.
/// </para>
/// <para>
/// ENGINEERING DECISION (ADR-0027, Accepted 2026-09-26): stored as <c>smallint</c> <c>0..1439</c>,
/// exchanged as <c>HH:mm</c> (R16). The pattern uses <c>[0-9]</c>, never <c>\d</c>, which in .NET
/// also matches Myanmar and other Unicode digits (plan R-12). Not an instant; ADR-0027 item 4's
/// "time to instant" function is not built until something consumes it.
/// </para>
/// </remarks>
public sealed partial record TimetableTime
{
    /// <summary>23:59, the last minute of the operating date (R15, R16).</summary>
    public const short MaxMinutes = 1439;

    private TimetableTime(short minutes)
    {
        Minutes = minutes;
    }

    public short Minutes { get; }

    /// <summary>
    /// A time from exactly <c>HH:mm</c> (<c>00</c>-<c>23</c>, <c>00</c>-<c>59</c>); anything else,
    /// including <c>null</c>, is <see cref="TimetableErrors.InvalidTimetableTime"/> (R16).
    /// </summary>
    public static Result<TimetableTime> Parse(string? text)
    {
        if (text is null || !Format().IsMatch(text))
        {
            return TimetableErrors.InvalidTimetableTime;
        }

        var hours = (text[0] - '0') * 10 + (text[1] - '0');
        var minutes = (text[3] - '0') * 10 + (text[4] - '0');
        return new TimetableTime((short)(hours * 60 + minutes));
    }

    /// <summary>A stored value. Throws outside <c>0..1439</c>: the database check refuses it too.</summary>
    internal static TimetableTime FromMinutes(short minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, MaxMinutes);
        return new TimetableTime(minutes);
    }

    /// <summary><c>HH:mm</c>, two digits each (R16).</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Minutes / 60:00}:{Minutes % 60:00}");

    // \A and \z, not ^ and $: $ also matches before a final newline.
    [GeneratedRegex(@"\A([01][0-9]|2[0-3]):[0-5][0-9]\z", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
