using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A route's human-facing code.
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-24; T-032, OQ41) — not a
/// Myanma Railways answer: a route code follows exactly the station-code rules, 2-10 characters of
/// <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c> (<see cref="NetworkCodeFormat"/>). It is unique across
/// all routes, including inactive ones, and never reused (R14; <c>UX_Routes_Code</c>). If Myanma
/// Railways later gives an official, different answer to OQ41, that supersedes this ruling and
/// needs its own follow-up task.
/// </remarks>
public sealed record RouteCode
{
    private RouteCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<RouteCode> Create(string? value)
    {
        var trimmed = value?.Trim();
        return NetworkCodeFormat.IsValid(trimmed)
            ? new RouteCode(trimmed!)
            : NetworkErrors.InvalidRouteCode;
    }

    internal static RouteCode From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new RouteCode(value);
    }

    public override string ToString() => Value;
}
