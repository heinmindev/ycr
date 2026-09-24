using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Identity;
using YCR.Application.Identity.Abstractions;
using YCR.Application.Identity.Login;
using YCR.Domain.Identity;

namespace YCR.Api.Tests.Authentication;

/// <summary>
/// Creates staff users and signs them in through the host's own services, so real-token tests
/// carry tokens the production issuer produced for sessions the production handler started.
/// </summary>
/// <remarks>
/// Users are created through the domain and <see cref="IPasswordService"/>, exactly as
/// <c>CreateUserHandler</c> does, so a test that is not about administration needs no
/// administrator; with the must-change flag cleared by default, as spec §4 assumes. The
/// administration endpoints themselves are tested through HTTP.
/// </remarks>
public static class StaffUserSeeder
{
    public const string Password = "kyauk.tan.12";

    public static async Task<Guid> SeedAsync(
        YcrApiFactory api,
        string userName,
        string[] roles,
        bool mustChangePassword = false,
        CancellationToken cancellationToken = default)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IIdentityDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var user = StaffUser.Create(Guid.CreateVersion7(), UserName.Create(userName).Value, clock.GetUtcNow());
        var roleIds = await db.Roles.Where(role => roles.Contains(role.Name)).Select(role => role.Id).ToListAsync(cancellationToken);
        if (roleIds.Count != roles.Length || user.ReplaceRoles(roleIds, callerId: null).IsFailure)
        {
            throw new InvalidOperationException("Unknown role in test seed.");
        }

        if (!await passwords.TrySetPasswordAsync(user, Password, cancellationToken))
        {
            throw new InvalidOperationException("The seed password was rejected.");
        }

        if (!mustChangePassword)
        {
            user.ChangeOwnPassword(clock.GetUtcNow());
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public static async Task<SignInResult> SignInAsync(YcrApiFactory api, string userName, CancellationToken cancellationToken = default)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<LoginHandler>()
            .Handle(new LoginCommand(userName, Password), cancellationToken);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Sign-in failed: {result.Error.Code}.");
    }
}
