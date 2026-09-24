namespace YCR.Application.Identity.Login;

/// <summary>
/// What a successful sign-in or refresh hands to the endpoint: the access token for the body and
/// the refresh token for the cookie (ADR-0016; spec R1, R5).
/// </summary>
/// <param name="AccessToken">The compact access JWT.</param>
/// <param name="AccessTokenExpiresAtUtc">The access token's <c>exp</c> (15 minutes, D12).</param>
/// <param name="RefreshToken">The raw refresh token, for the cookie only.</param>
/// <param name="SessionExpiresAtUtc">The session's absolute expiry: the cookie's <c>Expires</c>.</param>
public sealed record SignInResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset SessionExpiresAtUtc)
{
    /// <summary>Neither token may reach a log through this record (R13).</summary>
    public override string ToString() =>
        $"SignInResult {{ AccessToken = ***, AccessTokenExpiresAtUtc = {AccessTokenExpiresAtUtc:O}, RefreshToken = ***, SessionExpiresAtUtc = {SessionExpiresAtUtc:O} }}";
}
