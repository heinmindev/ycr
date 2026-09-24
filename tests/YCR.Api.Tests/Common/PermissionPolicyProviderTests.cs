using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using YCR.Api.Common.Authorization;
using YCR.Application.Common.Authorization;

namespace YCR.Api.Tests.Common;

/// <summary>
/// Plan F-002 §API changes, permission-name pattern: every approved permission, including the
/// multi-segment <c>users.roles.manage</c> and the hyphenated <c>auth-sessions.revoke</c>, gets a
/// permission policy; anything else still falls through to the default provider.
/// </summary>
public sealed class PermissionPolicyProviderTests
{
    private readonly PermissionPolicyProvider provider =
        new(Options.Create(new AuthorizationOptions()));

    [Theory]
    [InlineData(Permissions.StationsManage)]
    [InlineData(Permissions.StationsRead)]
    [InlineData(Permissions.UsersRead)]
    [InlineData(Permissions.UsersManage)]
    [InlineData(Permissions.UsersRolesManage)]
    [InlineData(Permissions.AuthSessionsRevoke)]
    public async Task GetPolicy_ForMultiSegmentAndHyphenatedPermission_ReturnsPermissionPolicy(string permission)
    {
        var policy = await provider.GetPolicyAsync(permission);

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(permission, requirement.Permission);
        Assert.Contains(policy.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Theory]
    [InlineData("stations")]
    [InlineData("Stations.Read")]
    [InlineData(".stations.read")]
    [InlineData("stations.read.")]
    [InlineData("stations..read")]
    [InlineData("-stations.read")]
    [InlineData("stations-.read")]
    [InlineData("auth--sessions.revoke")]
    [InlineData("stations.read1")]
    [InlineData("stations_read.x")]
    [InlineData("")]
    public async Task GetPolicy_ForNonPermissionName_FallsThrough(string policyName)
    {
        // The default provider knows no such named policy, so falling through yields null.
        var policy = await provider.GetPolicyAsync(policyName);

        Assert.Null(policy);
    }
}
