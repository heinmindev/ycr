namespace YCR.Api.Common.Authentication;

/// <summary>
/// The refresh-token cookie (ADR-0016; spec R5; D15): <c>HttpOnly; Secure; SameSite=Strict;
/// Path=/api/v1/auth/refresh</c>, expiring with the session (D12).
/// </summary>
/// <remarks>
/// The path scope means the browser sends the cookie to the refresh endpoint only — never to
/// logout or any other endpoint (C6). It is read inside the refresh endpoint, never by an
/// authentication scheme (D15).
/// </remarks>
public static class RefreshCookie
{
    public const string Name = "ycr_refresh";

    public const string Path = "/api/v1/auth/refresh";

    /// <summary>Sets the cookie to <paramref name="rawToken"/> until the session's absolute expiry.</summary>
    public static void Append(HttpResponse response, string rawToken, DateTimeOffset sessionExpiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Append(Name, rawToken, Options(sessionExpiresAtUtc));
    }

    /// <summary>Tells the browser to drop the cookie (logout, S15).</summary>
    public static void Expire(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, Options(expires: null));
    }

    /// <summary>The presented cookie, or null.</summary>
    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies[Name];
    }

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expires,
        IsEssential = true,
    };
}
