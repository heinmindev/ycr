using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// The canonical role identifiers (R19) for request validation in <c>YCR.Api</c>, which may not
/// reference <c>YCR.Domain.Identity</c> (plan P11 allowlist). Never used to authorize (R10).
/// </summary>
public static class RoleCatalogue
{
    public static IReadOnlyList<string> Names => RoleNames.All;

    /// <summary>Exact, ordinal match: role identifiers are case-sensitive, as seeded.</summary>
    public static bool IsKnown(string? name) => name is not null && RoleNames.All.Contains(name, StringComparer.Ordinal);
}
