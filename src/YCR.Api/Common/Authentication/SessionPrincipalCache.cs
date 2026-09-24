using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using YCR.Application.Identity;
using YCR.Application.Identity.ResolveSessionPrincipal;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// The per-instance cache ADR-0016 allows for session and permission lookups, bounded so that a
/// revocation or a permission change takes effect within the configured TTL, which startup
/// validation keeps at 30 seconds or less (R3; plan P4).
/// </summary>
/// <remarks>
/// An entry is reused only while <c>TimeProvider</c> says it is younger than
/// <c>Auth:PrincipalCacheSeconds</c>; after that the principal is resolved from the database
/// again. The session's absolute expiry is re-checked on every use, cached or not. The
/// <see cref="IMemoryCache"/> expiry (real time, TTL + 5 s) only bounds memory. The worst-case
/// latency is the TTL, per instance, however many instances run. A resolution that found nothing
/// (revoked, unknown, disabled) is cached too: those states do not come back.
/// Every hit records the entry's age on <c>ycr.auth.principal_cache.entry_age</c> (<c>docs/17</c>).
/// </remarks>
public sealed class SessionPrincipalCache(IMemoryCache cache, TimeProvider clock, IOptions<AuthOptions> options)
{
    public async Task<SessionPrincipal?> GetAsync(
        Guid userId,
        Guid sessionId,
        Func<Task<SessionPrincipal?>> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var now = clock.GetUtcNow();
        var ttl = TimeSpan.FromSeconds(options.Value.PrincipalCacheSeconds);
        var key = (typeof(SessionPrincipalCache), sessionId);

        if (cache.TryGetValue(key, out Entry? entry) && entry is not null && now - entry.LoadedAtUtc < ttl)
        {
            IdentityTelemetry.PrincipalCacheEntryAge.Record((now - entry.LoadedAtUtc).TotalSeconds);
        }
        else
        {
            entry = new Entry(await resolve(), now);
            cache.Set(key, entry, ttl + TimeSpan.FromSeconds(5));
        }

        var principal = entry.Principal;
        return principal is not null && principal.UserId == userId && now < principal.SessionExpiresAtUtc
            ? principal
            : null;
    }

    /// <summary>
    /// Drops a session's entry on this instance, so the session's own logout takes effect here at
    /// once. Other instances still converge within the TTL (R3).
    /// </summary>
    public void Evict(Guid sessionId) => cache.Remove((typeof(SessionPrincipalCache), sessionId));

    private sealed record Entry(SessionPrincipal? Principal, DateTimeOffset LoadedAtUtc);
}
