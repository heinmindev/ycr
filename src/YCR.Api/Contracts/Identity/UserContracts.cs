using FluentValidation;
using YCR.Application.Identity;
using YCR.Application.Identity.CreateUser;
using YCR.Application.Identity.GetUser;
using YCR.Application.Identity.ListRoles;
using YCR.Application.Identity.ListUserSessions;
using YCR.Application.Identity.ReplaceUserRoles;
using YCR.Application.Identity.ResetUserPassword;

namespace YCR.Api.Contracts.Identity;

/// <summary>The body of <c>POST /api/v1/users</c> (spec §6.2).</summary>
/// <remarks>
/// No actor, role-of-caller or permission field: those come from the server context only (R12,
/// S27). The username rule (R20) and the password policy (R18) stay in the domain and the
/// password validator; this contract checks shape and the role catalogue only.
/// </remarks>
public sealed record CreateUserRequest(string? UserName, string? Password, IReadOnlyList<string>? Roles)
{
    public CreateUserCommand ToCommand() => new(UserName ?? string.Empty, Password ?? string.Empty, Roles ?? []);

    /// <summary>Neither the password nor the username reaches a log through this record (R13, D6).</summary>
    public override string ToString() => $"CreateUserRequest {{ UserName = ***, Password = ***, Roles = [{string.Join(", ", Roles ?? [])}] }}";
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.UserName).NotEmpty();
        RuleFor(request => request.Password).NotEmpty();
        RuleFor(request => request.Roles).NotNull();
        RuleForEach(request => request.Roles).Must(RoleCatalogue.IsKnown).WithMessage("'{PropertyValue}' is not a role (R19).");
    }
}

/// <summary>The body of <c>POST /api/v1/users</c>'s <c>201</c>.</summary>
public sealed record CreateUserResponse(Guid Id);

/// <summary>The body of <c>PUT /api/v1/users/{id}/roles</c>: the complete new role set.</summary>
public sealed record ReplaceUserRolesRequest(IReadOnlyList<string>? Roles)
{
    public ReplaceUserRolesCommand ToCommand(Guid userId) => new(userId, Roles ?? []);
}

/// <summary>Roles must be present (an empty list removes every role) and each one known (R19).</summary>
public sealed class ReplaceUserRolesRequestValidator : AbstractValidator<ReplaceUserRolesRequest>
{
    public ReplaceUserRolesRequestValidator()
    {
        RuleFor(request => request.Roles).NotNull();
        RuleForEach(request => request.Roles).Must(RoleCatalogue.IsKnown).WithMessage("'{PropertyValue}' is not a role (R19).");
    }
}

/// <summary>The body of <c>POST /api/v1/users/{id}/password-reset</c>.</summary>
public sealed record ResetUserPasswordRequest(string? NewPassword)
{
    public ResetUserPasswordCommand ToCommand(Guid userId) => new(userId, NewPassword ?? string.Empty);

    public override string ToString() => "ResetUserPasswordRequest { NewPassword = *** }";
}

/// <summary>Presence only; the policy is <c>400 Identity.PasswordRejected</c> from the handler.</summary>
public sealed class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
    public ResetUserPasswordRequestValidator() => RuleFor(request => request.NewPassword).NotEmpty();
}

/// <summary>
/// A staff account (spec §6.2): username, roles, disabled, locked-until (only while a lockout is
/// in force), created. Never a hash, stamp or token.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string UserName,
    IReadOnlyList<string> Roles,
    bool IsDisabled,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset CreatedAtUtc)
{
    public static UserResponse From(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserResponse(user.Id, user.UserName, user.Roles, user.IsDisabled, user.LockedUntilUtc, user.CreatedAtUtc);
    }
}

/// <summary>A session as the administration API shows it (spec §6.2): never a token or hash.</summary>
public sealed record AuthSessionResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason)
{
    public static AuthSessionResponse From(AuthSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new AuthSessionResponse(session.Id, session.CreatedAtUtc, session.ExpiresAtUtc, session.RevokedAtUtc, session.RevocationReason);
    }
}

/// <summary>A role and the permissions it grants (<c>GET /api/v1/roles</c>).</summary>
public sealed record RoleResponse(string Name, IReadOnlyList<string> Permissions)
{
    public static RoleResponse From(RoleDto role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return new RoleResponse(role.Name, role.Permissions);
    }
}
