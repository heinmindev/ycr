using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace YCR.Application.Identity;

/// <summary>
/// Identity log events and metrics (<c>docs/17</c>; D6, C11: the <c>Auth*</c> names are log and
/// metric names, not audit actions).
/// </summary>
/// <remarks>
/// REQUIRED CONTROL (<c>docs/20</c> §7; R13; S28): nothing here carries a username, a password,
/// a token, a token hash or a cookie. Session ids are opaque and may be logged; user ids are not
/// logged.
/// </remarks>
public static class IdentityTelemetry
{
    public const string MeterName = "YCR.Identity";

    public const string LoginSucceededInstrument = "ycr.auth.login_succeeded";
    public const string LoginFailedInstrument = "ycr.auth.login_failed";
    public const string RefreshSupersededInstrument = "ycr.auth.refresh_superseded";
    public const string RefreshFamilyRevokedInstrument = "ycr.auth.refresh_family_revoked";

    /// <summary>
    /// <c>docs/17</c> "session/permission cache revocation latency": the age of a cached principal
    /// each time it is used (plan P4). Recorded by the API's principal cache.
    /// </summary>
    public const string PrincipalCacheEntryAgeInstrument = "ycr.auth.principal_cache.entry_age";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> LoginSucceeded = Meter.CreateCounter<long>(LoginSucceededInstrument);
    public static readonly Counter<long> LoginFailed = Meter.CreateCounter<long>(LoginFailedInstrument);
    public static readonly Counter<long> RefreshSuperseded = Meter.CreateCounter<long>(RefreshSupersededInstrument);
    public static readonly Counter<long> RefreshFamilyRevoked = Meter.CreateCounter<long>(RefreshFamilyRevokedInstrument);

    public static readonly Histogram<double> PrincipalCacheEntryAge =
        Meter.CreateHistogram<double>(PrincipalCacheEntryAgeInstrument, unit: "s");

    public static readonly Action<ILogger, Exception?> LogLoginSucceeded = LoggerMessage.Define(
        LogLevel.Information, new EventId(2001, "AuthLoginSucceeded"), "Sign-in succeeded.");

    public static readonly Action<ILogger, Exception?> LogLoginFailed = LoggerMessage.Define(
        LogLevel.Information, new EventId(2002, "AuthLoginFailed"), "Sign-in failed.");

    public static readonly Action<ILogger, Guid, Exception?> LogRefreshSuperseded = LoggerMessage.Define<Guid>(
        LogLevel.Information,
        new EventId(2003, "AuthRefreshSuperseded"),
        "Refresh token presented again within the grace window for session {SessionId}; answered 409.");

    public static readonly Action<ILogger, Guid, Exception?> LogRefreshFamilyRevoked = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(2004, "AuthRefreshFamilyRevoked"),
        "Refresh token reuse outside the grace window; session {SessionId} revoked.");
}
