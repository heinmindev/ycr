using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A station or route name in English and Myanmar Unicode.
/// </summary>
/// <remarks>
/// DECISION (tech lead, hein, 2026-09-22; T-014) - final; not a Myanma Railways answer:
/// both names are required and contain 1-100 characters after trimming. If Myanma Railways
/// later gives an official, different answer to OQ27, that supersedes this ruling and
/// needs its own follow-up task.
/// <para>
/// Route names follow the same rule: BUSINESS DECISION — provisional tech-lead ruling (hein,
/// 2026-09-24; T-032, OQ41) — not a Myanma Railways answer. Only the error differs
/// (<c>Network.InvalidRouteName</c>), so a route passes it through
/// <see cref="Create(string?, string?, Error)"/> (F-003 plan P3).
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

    /// <summary>A station name; an invalid one is <see cref="NetworkErrors.InvalidStationName"/>.</summary>
    public static Result<BilingualName> Create(string? en, string? my) =>
        Create(en, my, NetworkErrors.InvalidStationName);

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
