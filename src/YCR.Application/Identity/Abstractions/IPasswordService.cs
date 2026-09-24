using YCR.Domain.Identity;

namespace YCR.Application.Identity.Abstractions;

/// <summary>
/// Password verification and the password policy, over ASP.NET Core Identity (ADR-0023 items 1–2;
/// plan P1). Implemented in Infrastructure so that Identity never reaches the Application layer.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Verifies <paramref name="password"/> for <paramref name="user"/>. When
    /// <paramref name="user"/> is null (unknown username) a full hash verification still runs
    /// against a precomputed dummy hash and the result is <see langword="false"/>, so the unknown
    /// path costs the same as the known one (R23, S2). Disabled and locked users are verified in
    /// full too; the caller decides what the result means for them.
    /// </summary>
    Task<bool> VerifyAsync(StaffUser? user, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks <paramref name="password"/> against the policy (12–128 characters, not on the
    /// blocklist — D2, R18) and, if it passes, applies its hash and a new security stamp to
    /// <paramref name="user"/>. Nothing is saved; the caller's unit of work does that.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the policy rejects the password; the user is unchanged. The
    /// caller maps that to the error code of its own path (U1).
    /// </returns>
    Task<bool> TrySetPasswordAsync(StaffUser user, string password, CancellationToken cancellationToken);
}
