using YCR.Application.Common.Abstractions;

namespace YCR.Application.Identity;

/// <summary>
/// A user's role set before or after <c>Identity.RolesChanged</c> (spec S18: before
/// <c>["StationManager"]</c>, after <c>[]</c>). <c>PayloadVersion</c> 1.
/// </summary>
/// <param name="Roles">Canonical role identifiers (R19), in catalogue order.</param>
public sealed record UserRolesAuditSnapshot(IReadOnlyList<string> Roles) : IAuditSnapshot;
