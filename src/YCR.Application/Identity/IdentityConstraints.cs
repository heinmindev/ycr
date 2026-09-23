namespace YCR.Application.Identity;

/// <summary>
/// Database constraint names the Identity handlers match on, so a unique violation becomes a
/// typed error instead of a <c>500</c> (the <c>NetworkConstraints</c> pattern). The EF
/// configuration uses these same constants, and a model test pins them.
/// </summary>
public static class IdentityConstraints
{
    /// <summary>Duplicate username → <c>409 Identity.UserNameAlreadyExists</c> (S32a).</summary>
    public const string UserNameUniqueIndex = "UX_Users_NormalizedUserName";

    /// <summary>A refresh token has at most one successor (R6, S33; plan P15).</summary>
    public const string RefreshTokenSuccessorUniqueIndex = "UX_RefreshTokens_ReplacedByTokenId";

    public const string RefreshTokenHashUniqueIndex = "UX_RefreshTokens_TokenHash";
}
