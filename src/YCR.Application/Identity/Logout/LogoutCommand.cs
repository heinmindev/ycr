namespace YCR.Application.Identity.Logout;

/// <summary>
/// Sign out the session named by the access token's <c>sid</c> (spec §6.1 <c>POST /auth/logout</c>;
/// D12, C6: the path-scoped refresh cookie never reaches this endpoint).
/// </summary>
public sealed record LogoutCommand(Guid SessionId);
