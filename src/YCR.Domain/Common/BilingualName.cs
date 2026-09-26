namespace YCR.Domain.Common;

/// <summary>
/// A name in English and Myanmar Unicode: a station's, a route's or a service's.
/// </summary>
/// <remarks>
/// DECISION (tech lead, hein, 2026-09-22; T-014) - final; not a Myanma Railways answer:
/// both names are required and contain 1-100 characters after trimming. If Myanma Railways
/// later gives an official, different answer to OQ27, that supersedes this ruling and
/// needs its own follow-up task.
/// <para>
/// Route names follow the same rule: BUSINESS DECISION — provisional tech-lead ruling (hein,
/// 2026-09-24; T-032, OQ41) — not a Myanma Railways answer. So do service names: BUSINESS
/// DECISION — provisional tech-lead ruling (hein, 2026-09-25; T-044, OQ43) — not a Myanma
/// Railways answer.
/// </para>
/// <para>
/// ENGINEERING DECISION (F-004 plan P2, ruling Q1, tech lead, hein, 2026-09-25): the type lives in
/// the shared kernel, and every caller passes its own module's error
/// (<c>Network.InvalidStationName</c>, <c>Network.InvalidRouteName</c>,
/// <c>Timetable.InvalidServiceName</c>), so the kernel names no module.
/// </para>
/// </remarks>
public sealed record BilingualName
{
    private BilingualName(string en, string my)
    {
        En = en;
        My = my;
    }

    public string En { get; }

    public string My { get; }

    /// <summary>A name whose owner supplies the error to return when it is invalid.</summary>
    public static Result<BilingualName> Create(string? en, string? my, Error whenInvalid)
    {
        ArgumentNullException.ThrowIfNull(whenInvalid);

        var trimmedEn = en?.Trim();
        var trimmedMy = my?.Trim();

        return IsValid(trimmedEn) && IsValid(trimmedMy)
            ? new BilingualName(trimmedEn!, trimmedMy!)
            : whenInvalid;
    }

    private static bool IsValid(string? value) => value is { Length: >= 1 and <= 100 };
}
