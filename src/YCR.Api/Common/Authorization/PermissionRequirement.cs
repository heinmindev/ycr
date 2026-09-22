using Microsoft.AspNetCore.Authorization;

namespace YCR.Api.Common.Authorization;

/// <summary>Requires the caller to hold <paramref name="Permission"/> (docs/10, ADR-0016).</summary>
/// <param name="Permission">A permission name such as <c>stations.manage</c> (docs/20 §2).</param>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
