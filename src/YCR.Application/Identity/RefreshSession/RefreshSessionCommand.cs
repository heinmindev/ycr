namespace YCR.Application.Identity.RefreshSession;

/// <summary>
/// Rotate the refresh token read from the cookie inside the endpoint (spec §6.1
/// <c>POST /auth/refresh</c>; R5, D15). Null when the request carried no cookie.
/// </summary>
public sealed record RefreshSessionCommand(string? RefreshToken)
{
    public override string ToString() => "RefreshSessionCommand { RefreshToken = *** }";
}
