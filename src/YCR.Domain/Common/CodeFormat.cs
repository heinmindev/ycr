using System.Text.RegularExpressions;

namespace YCR.Domain.Common;

/// <summary>
/// The one code format shared by station codes, route codes and service codes: 2-10 characters,
/// <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c> only.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (F-003 plan P2; F-004 plan P2, ruling Q1, tech lead, hein, 2026-09-25):
/// one rule, one home, in the shared kernel so that no module's domain depends on another's. Each
/// value object keeps its own error code (<c>Network.InvalidStationCode</c>,
/// <c>Network.InvalidRouteCode</c>, <c>Timetable.InvalidServiceCode</c>), so only the pattern is
/// shared. The rulings behind it have different standing:
/// <list type="bullet">
/// <item>station codes — DECISION (tech lead, hein, 2026-09-22; T-014), final; not a Myanma
/// Railways answer (OQ26);</item>
/// <item>route codes — BUSINESS DECISION, provisional tech-lead ruling (hein, 2026-09-24; T-032,
/// OQ41), not a Myanma Railways answer: "exactly the station rules";</item>
/// <item>service codes — BUSINESS DECISION, provisional tech-lead ruling (hein, 2026-09-25; T-044,
/// OQ43), not a Myanma Railways answer: under the station and route rules.</item>
/// </list>
/// If Myanma Railways later gives the codes different answers, splitting this class is a local
/// change.
/// </remarks>
internal static class CodeFormat
{
    private static readonly Regex ValidPattern = new(
        "^[A-Z0-9]{2,10}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Whether an already-trimmed <paramref name="value"/> is a valid code.</summary>
    public static bool IsValid(string? value) => value is not null && ValidPattern.IsMatch(value);
}
