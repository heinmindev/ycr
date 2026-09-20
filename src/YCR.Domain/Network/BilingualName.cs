using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>
/// A station name in English and Myanmar Unicode.
/// </summary>
/// <remarks>
/// ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered):
/// both names are required and contain 1-100 characters after trimming.
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

    public static Result<BilingualName> Create(string? en, string? my)
    {
        var trimmedEn = en?.Trim();
        var trimmedMy = my?.Trim();

        return IsValid(trimmedEn) && IsValid(trimmedMy)
            ? new BilingualName(trimmedEn!, trimmedMy!)
            : NetworkErrors.InvalidStationName;
    }

    private static bool IsValid(string? value) => value is { Length: >= 1 and <= 100 };
}
