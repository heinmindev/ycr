using YCR.Domain.Common;
using YCR.Domain.Identity;

namespace YCR.Application.Identity;

/// <summary>
/// The MFA release gate enforced in code (ADR-0023 item 4, amended 2026-09-24; review S-1): in the
/// <c>Production</c> environment no account is created with, and no account is granted,
/// <c>SystemAdministrator</c>, <c>RailwayAdministrator</c> or <c>FinanceOfficer</c> — through the
/// API or the bootstrap CLI — until the MFA feature (T-026 item 1) ships and removes this gate.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (hein, 2026-09-24; T-030, S-1). Each host registers the gate for its own
/// environment before <c>AddApplication</c>; <c>AddApplication</c>'s fallback blocks, so a host that
/// forgets fails closed. Only granting is refused: a role set that keeps a privileged role the user
/// already holds is not an assignment.
/// </remarks>
public sealed class PrivilegedRoleGate(bool blocksPrivilegedRoles)
{
    /// <summary>The environment the gate applies to (the <c>IHostEnvironment.IsProduction</c> name).</summary>
    public const string ProductionEnvironment = "Production";

    /// <summary>The roles ADR-0023 item 4 keeps out of production until MFA ships.</summary>
    public static IReadOnlyList<string> PrivilegedRoles { get; } =
    [
        RoleNames.SystemAdministrator,
        RoleNames.RailwayAdministrator,
        RoleNames.FinanceOfficer,
    ];

    /// <summary>True when privileged roles may not be granted in this process.</summary>
    public bool BlocksPrivilegedRoles { get; } = blocksPrivilegedRoles;

    /// <summary>The gate for a host environment: blocking in <c>Production</c> only (case-insensitive, as the host compares it).</summary>
    public static PrivilegedRoleGate ForEnvironment(string environmentName) =>
        new(string.Equals(environmentName, ProductionEnvironment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Refuses with <see cref="IdentityErrors.PrivilegedRoleRequiresMfa"/> when this process blocks
    /// privileged roles and <paramref name="grantedRoleNames"/> — the roles being newly granted —
    /// contains one.
    /// </summary>
    public Result Check(IEnumerable<string> grantedRoleNames)
    {
        ArgumentNullException.ThrowIfNull(grantedRoleNames);

        return BlocksPrivilegedRoles && grantedRoleNames.Any(role => PrivilegedRoles.Contains(role, StringComparer.Ordinal))
            ? IdentityErrors.PrivilegedRoleRequiresMfa
            : Result.Success();
    }
}
