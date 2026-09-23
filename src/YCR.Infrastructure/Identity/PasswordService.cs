using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using YCR.Application.Identity.Abstractions;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// Password verification and policy over <see cref="UserManager{TUser}"/> (ADR-0023 items 1–3;
/// plan P1).
/// </summary>
/// <remarks>
/// R23 / S2 (equalised timing): every call to <see cref="VerifyAsync"/> performs one full hash
/// verification with the registered hasher — against the user's hash when the user exists
/// (disabled and locked users included), and against a precomputed dummy hash when it does not.
/// The dummy is hashed with Identity's default options, which are the options the registered
/// hasher uses, so both paths cost the same PBKDF2 work.
/// </remarks>
internal sealed class PasswordService(UserManager<StaffUser> userManager) : IPasswordService
{
    private static readonly StaffUser UnknownUser =
        StaffUser.Create(Guid.Empty, UserName.Create("unknown.user").Value, DateTimeOffset.UnixEpoch);

    /// <summary>A hash of a random value nobody knows, computed once per process.</summary>
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<StaffUser>(Options.Create(new PasswordHasherOptions()))
            .HashPassword(UnknownUser, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    public async Task<bool> VerifyAsync(StaffUser? user, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (user is null)
        {
            userManager.PasswordHasher.VerifyHashedPassword(UnknownUser, DummyHash.Value, password);
            return false;
        }

        // UserManager verifies and, when the stored hash uses older parameters, re-hashes it on
        // the tracked user (saved by the caller's unit of work).
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<bool> TrySetPasswordAsync(StaffUser user, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);
            if (!result.Succeeded)
            {
                return false;
            }
        }

        user.ApplyPasswordHash(userManager.PasswordHasher.HashPassword(user, password));
        user.ApplySecurityStamp(Convert.ToHexString(RandomNumberGenerator.GetBytes(20)));
        return true;
    }
}
