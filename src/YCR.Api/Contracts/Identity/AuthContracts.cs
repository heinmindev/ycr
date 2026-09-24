using FluentValidation;
using YCR.Application.Identity.ChangeOwnPassword;
using YCR.Application.Identity.GetCurrentUser;
using YCR.Application.Identity.Login;

namespace YCR.Api.Contracts.Identity;

/// <summary>The body of <c>POST /api/v1/auth/login</c> (spec §6.1).</summary>
/// <remarks>
/// Carries no actor or role field: the audit actor of a sign-in is the user whose password was
/// just verified, never anything the request names (R12, S27).
/// </remarks>
public sealed record LoginRequest(string? UserName, string? Password)
{
    public LoginCommand ToCommand() => new(UserName ?? string.Empty, Password ?? string.Empty);

    /// <summary>The password never reaches a log through this record (R13).</summary>
    public override string ToString() => "LoginRequest { UserName = ***, Password = *** }";
}

/// <summary>
/// Shape only: both fields present. Whether they are right is the handler's uniform
/// <c>401 Auth.InvalidCredentials</c> (R23); the username format is not checked here, so a
/// malformed name is indistinguishable from an unknown one.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.UserName).NotEmpty();
        RuleFor(request => request.Password).NotEmpty();
    }
}

/// <summary>
/// The body of a successful sign-in or refresh: the access token only (ADR-0016). The refresh
/// token travels in the cookie, never in a body.
/// </summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAtUtc)
{
    public static AccessTokenResponse From(SignInResult signIn)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        return new AccessTokenResponse(signIn.AccessToken, signIn.AccessTokenExpiresAtUtc);
    }

    public override string ToString() => $"AccessTokenResponse {{ AccessToken = ***, ExpiresAtUtc = {ExpiresAtUtc:O} }}";
}

/// <summary><c>GET /api/v1/auth/me</c> (spec §6.1): no display name, no must-change flag (U2).</summary>
public sealed record CurrentUserResponse(Guid UserId, string UserName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public static CurrentUserResponse From(CurrentUserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new CurrentUserResponse(user.UserId, user.UserName, user.Roles, user.Permissions);
    }
}

/// <summary>The body of <c>POST /api/v1/auth/password</c> (spec §6.1).</summary>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword)
{
    public ChangeOwnPasswordCommand ToCommand(Guid sessionId) =>
        new(sessionId, CurrentPassword ?? string.Empty, NewPassword ?? string.Empty);

    public override string ToString() => "ChangePasswordRequest { CurrentPassword = ***, NewPassword = *** }";
}

/// <summary>
/// Shape only: both fields present. The password policy (R18) is the handler's
/// <c>400 Auth.PasswordRejected</c>, so the policy has one home (<c>PasswordPolicyValidator</c>).
/// </summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty();
        RuleFor(request => request.NewPassword).NotEmpty();
    }
}
