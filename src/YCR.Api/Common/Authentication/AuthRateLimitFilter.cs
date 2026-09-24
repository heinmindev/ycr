using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using YCR.Api.Contracts.Identity;

namespace YCR.Api.Common.Authentication;

/// <summary>
/// The partitioned limiters behind R16 (D5, U6; plan P6), one singleton per host, so the limits
/// are in process and per instance (accepted, C13).
/// </summary>
/// <remarks>
/// Sliding windows of one minute in six segments: "N per minute" holds across any 60-second span,
/// not only within fixed minutes, which would let 2N through across a boundary. No queueing: an
/// attempt over the limit is refused at once. The windows run on real time, not
/// <see cref="TimeProvider"/> (R-3); tests fire bursts well inside one window.
/// </remarks>
public sealed class AuthRateLimiters : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public AuthRateLimiters(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Value.RateLimits;
        LoginPerUserName = Create(limits.LoginPerUserNamePerMinute);
        LoginPerClientAddress = Create(limits.LoginPerClientAddressPerMinute);
        RefreshPerClientAddress = Create(limits.RefreshPerClientAddressPerMinute);
    }

    public PartitionedRateLimiter<string> LoginPerUserName { get; }

    public PartitionedRateLimiter<string> LoginPerClientAddress { get; }

    public PartitionedRateLimiter<string> RefreshPerClientAddress { get; }

    public void Dispose()
    {
        LoginPerUserName.Dispose();
        LoginPerClientAddress.Dispose();
        RefreshPerClientAddress.Dispose();
    }

    private static PartitionedRateLimiter<string> Create(int permitsPerMinute) =>
        PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetSlidingWindowLimiter(
            key,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = Window,
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
}

/// <summary>
/// Applies R16's limits to one cookie endpoint as an endpoint filter (plan P6), after the
/// <see cref="OriginCheckFilter"/> and before validation and the handler, so a refused request
/// evaluates no credential, writes no audit row and counts no failure (S5).
/// </summary>
/// <remarks>
/// A filter rather than the ASP.NET Core rate-limiting middleware because the login's username
/// partition comes from the bound body, which the middleware's synchronous partitioner cannot
/// read. Refusal is <c>429 Auth.TooManyRequests</c>.
/// </remarks>
public abstract class AuthRateLimitFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var (limiter, key) in Partitions(context))
        {
            using var lease = limiter.AttemptAcquire(key);
            if (!lease.IsAcquired)
            {
                return AuthProblem.Result(
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    "Too many attempts. Try again later.",
                    AuthErrorCodes.TooManyRequests);
            }
        }

        return await next(context);
    }

    /// <summary>The limiters to pass, in order; the first refusal stops the request.</summary>
    protected abstract IEnumerable<(PartitionedRateLimiter<string> Limiter, string Key)> Partitions(EndpointFilterInvocationContext context);

    /// <summary>
    /// The connection's address. No forwarded-headers handling exists until hosting is decided, so
    /// behind a proxy this is the proxy (R16, O7 — accepted).
    /// </summary>
    protected static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>Login: per client address, then per normalized username.</summary>
public sealed class LoginRateLimitFilter(AuthRateLimiters limiters) : AuthRateLimitFilter
{
    /// <summary>R20's longest username; a longer value cannot name an account and shares one partition.</summary>
    private const int MaximumUserNameLength = 50;

    protected override IEnumerable<(PartitionedRateLimiter<string> Limiter, string Key)> Partitions(EndpointFilterInvocationContext context)
    {
        yield return (limiters.LoginPerClientAddress, ClientAddress(context.HttpContext));

        var userName = context.Arguments.OfType<LoginRequest>().FirstOrDefault()?.UserName ?? string.Empty;
        // The same normalization the sign-in lookup uses, so "Hein.Min" and "hein.min" share a
        // partition; overlong values share one, so they cannot grow the partition table (R-10).
        yield return (limiters.LoginPerUserName, userName.Length > MaximumUserNameLength ? "\u0000overlong" : userName.ToUpperInvariant());
    }
}

/// <summary>Refresh: per client address only.</summary>
public sealed class RefreshRateLimitFilter(AuthRateLimiters limiters) : AuthRateLimitFilter
{
    protected override IEnumerable<(PartitionedRateLimiter<string> Limiter, string Key)> Partitions(EndpointFilterInvocationContext context)
    {
        yield return (limiters.RefreshPerClientAddress, ClientAddress(context.HttpContext));
    }
}
