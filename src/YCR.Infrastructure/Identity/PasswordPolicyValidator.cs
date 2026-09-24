using Microsoft.AspNetCore.Identity;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// The password policy (ADR-0023 item 2; spec R18): 12–128 characters, no composition rules, not
/// on the offline blocklist. ENGINEERING DECISION (hein, 2026-09-23; D2). OQ35: no mandated
/// policy is known.
/// </summary>
/// <remarks>
/// The only <see cref="IPasswordValidator{TUser}"/> registered: Identity's default composition
/// validator is removed (D2 forbids composition rules). Length is counted in Unicode scalar
/// values, not UTF-16 units, so a Myanmar or emoji password does not count double through
/// surrogates (plan P12). Nothing is normalised. The errors name no part of the password.
/// </remarks>
internal sealed class PasswordPolicyValidator(CommonPasswordBlocklist blocklist) : IPasswordValidator<StaffUser>
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public Task<IdentityResult> ValidateAsync(UserManager<StaffUser> manager, StaffUser user, string? password)
    {
        if (password is null)
        {
            return Task.FromResult(Failed("PasswordRequired", "A password is required."));
        }

        var length = password.EnumerateRunes().Count();
        if (length < MinimumLength)
        {
            return Task.FromResult(Failed("PasswordTooShort", $"A password has at least {MinimumLength} characters."));
        }

        if (length > MaximumLength)
        {
            return Task.FromResult(Failed("PasswordTooLong", $"A password has at most {MaximumLength} characters."));
        }

        return Task.FromResult(blocklist.Contains(password)
            ? Failed("PasswordTooCommon", "The password is too common.")
            : IdentityResult.Success);
    }

    private static IdentityResult Failed(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });
}
