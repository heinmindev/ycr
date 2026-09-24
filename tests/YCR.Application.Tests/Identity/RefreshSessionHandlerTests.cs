using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Identity;
using YCR.Application.Identity.RefreshSession;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.Domain.Identity;
using SignInResult = YCR.Application.Identity.Login.SignInResult;

namespace YCR.Application.Tests.Identity;

/// <summary>
/// Refresh rotation (ADR-0016 §Required tests; spec R5–R8; S7–S11, S13, S14, S27, S29, S33).
/// </summary>
public sealed class RefreshSessionHandlerTests(SqlServerFixture fixture) : IdentityHandlerTestBase(fixture)
{
    protected override string DatabasePrefix => "refresh";

    [Fact]
    public async Task Handle_WithCurrentToken_RotatesAndKeepsSessionExpiry()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);
        Clock.Advance(TimeSpan.FromMinutes(14));

        var result = await RefreshAsync(provider, login.RefreshToken);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(login.RefreshToken, result.Value.RefreshToken);
        Assert.Equal(login.SessionExpiresAtUtc, result.Value.SessionExpiresAtUtc);
        Assert.Equal(Clock.GetUtcNow().AddMinutes(15), result.Value.AccessTokenExpiresAtUtc);
        Assert.Equal(session, FakeAccessTokenIssuer.Parse(result.Value.AccessToken).SessionId);
        Assert.Equal(2, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}';"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}' AND [RotatedAtUtc] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL;"));
        // S7: rotation is not an audit event.
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
    }

    /// <summary>S8 / S33: two tabs, one token — one rotation, one 409, family intact, successor works.</summary>
    [Fact]
    public async Task Handle_WithParallelSameToken_OneRotatesOneSuperseded()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);

        var results = await Task.WhenAll(RefreshAsync(provider, login.RefreshToken), RefreshAsync(provider, login.RefreshToken));

        var winner = Assert.Single(results, result => result.IsSuccess);
        var loser = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(IdentityErrors.RefreshSuperseded, loser.Error);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}' AND [ReplacedByTokenId] IS NOT NULL;"));
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{session}' AND [RevokedAtUtc] IS NULL;"));

        Assert.True((await RefreshAsync(provider, winner.Value.RefreshToken)).IsSuccess);
    }

    /// <summary>S9: within the grace window — 409, no tokens, no revocation, logged and counted, not audited.</summary>
    [Fact]
    public async Task Handle_PredecessorWithinGrace_ReturnsSupersededAndWritesNoAudit()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);
        Assert.True((await RefreshAsync(provider, login.RefreshToken)).IsSuccess);
        var auditBefore = await AuditCountAsync();
        Clock.Advance(TimeSpan.FromSeconds(20));

        long superseded = 0;
        using (var listener = Listen(IdentityTelemetry.RefreshSupersededInstrument, value => Interlocked.Add(ref superseded, value)))
        {
            var result = await RefreshAsync(provider, login.RefreshToken);
            Assert.Equal(IdentityErrors.RefreshSuperseded, result.Error);
        }

        Assert.True(superseded >= 1);
        Assert.Equal(auditBefore, await AuditCountAsync());
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[AuthSessions] WHERE [Id] = '{session}' AND [RevokedAtUtc] IS NULL;"));
        Assert.Equal(2, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}';"));
    }

    /// <summary>S10 / S27 / G1: after the window the family is revoked with one audit row, no actor, user subject.</summary>
    [Fact]
    public async Task Handle_PredecessorAfterGrace_RevokesFamilyAndAuditsOnce()
    {
        var ambient = new TestCurrentUser { UserId = Guid.NewGuid(), Roles = [RoleNames.SystemAdministrator], CorrelationId = "corr-refresh" };
        await using var provider = BuildProvider(ambient);
        var (session, login) = await SignInAsync(provider);
        var rotated = await RefreshAsync(provider, login.RefreshToken);
        Clock.Advance(TimeSpan.FromSeconds(21));

        var result = await RefreshAsync(provider, login.RefreshToken);

        Assert.Equal(IdentityErrors.RefreshInvalid, result.Error);
        Assert.Equal("FamilyReuse", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{session}';"));
        var row = Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
        Assert.Null(row.ActorUserId);
        Assert.Null(row.ActorRole);
        Assert.Equal(IdentityAuditSubjects.User, row.SubjectType);
        Assert.Equal(await ScalarAsync<Guid>($"SELECT [UserId] FROM [identity].[AuthSessions] WHERE [Id] = '{session}';"), row.SubjectId);
        Assert.Contains($"\"sessionId\":\"{session}\"", row.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"revocationReason\":\"FamilyReuse\"", row.AfterJson, StringComparison.Ordinal);

        // The successor dies with the family, and a second reuse is not audited again.
        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, rotated.Value.RefreshToken)).Error);
        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, login.RefreshToken)).Error);
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
    }

    /// <summary>S11: an older ancestor revokes the family even inside the grace window.</summary>
    [Fact]
    public async Task Handle_Ancestor_RevokesFamily()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);
        var second = await RefreshAsync(provider, login.RefreshToken);
        Assert.True((await RefreshAsync(provider, second.Value.RefreshToken)).IsSuccess);

        var result = await RefreshAsync(provider, login.RefreshToken);

        Assert.Equal(IdentityErrors.RefreshInvalid, result.Error);
        Assert.Equal("FamilyReuse", await ScalarAsync<string>($"SELECT [RevocationReason] FROM [identity].[AuthSessions] WHERE [Id] = '{session}';"));
        Assert.Single(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
    }

    /// <summary>S13: the 12-hour absolute lifetime — no rotation after it.</summary>
    [Fact]
    public async Task Handle_AfterSessionLifetime_ReturnsRefreshInvalid()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);
        Clock.Advance(TimeSpan.FromHours(12));

        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, login.RefreshToken)).Error);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}';"));
        Assert.Empty(await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked));
    }

    /// <summary>S14: revoked session, missing cookie, unknown token.</summary>
    [Fact]
    public async Task Handle_RevokedSessionOrUnknownToken_ReturnsRefreshInvalid()
    {
        await using var provider = BuildProvider();
        var (session, login) = await SignInAsync(provider);

        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, null)).Error);
        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, string.Empty)).Error);
        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, "not-a-token-anyone-issued")).Error);

        await ExecuteAsMigratorAsync($"UPDATE [identity].[AuthSessions] SET [RevokedAtUtc] = SYSUTCDATETIME() AT TIME ZONE 'UTC', [RevocationReason] = N'AdministratorRevoked' WHERE [Id] = '{session}';");
        Assert.Equal(IdentityErrors.RefreshInvalid, (await RefreshAsync(provider, login.RefreshToken)).Error);
        Assert.Equal(1, await ScalarAsync<int>($"SELECT COUNT(*) FROM [identity].[RefreshTokens] WHERE [SessionId] = '{session}';"));
    }

    /// <summary>S29: across S8–S11 the ledger holds exactly the two family revocations.</summary>
    [Fact]
    public async Task Ledger_AfterRefreshScenarios_HasExactlyTwoFamilyRevokedRows()
    {
        await using var provider = BuildProvider();
        await SeedUserAsync(provider, "tab.user", [RoleNames.TicketOperator]);

        // S8
        var s8 = (await LoginAsync(provider, "tab.user")).Value;
        await Task.WhenAll(RefreshAsync(provider, s8.RefreshToken), RefreshAsync(provider, s8.RefreshToken));
        // S9
        var s9 = (await LoginAsync(provider, "tab.user")).Value;
        await RefreshAsync(provider, s9.RefreshToken);
        await RefreshAsync(provider, s9.RefreshToken);
        // S10
        var s10 = (await LoginAsync(provider, "tab.user")).Value;
        await RefreshAsync(provider, s10.RefreshToken);
        Clock.Advance(TimeSpan.FromSeconds(21));
        await RefreshAsync(provider, s10.RefreshToken);
        // S11
        var s11 = (await LoginAsync(provider, "tab.user")).Value;
        var next = (await RefreshAsync(provider, s11.RefreshToken)).Value;
        await RefreshAsync(provider, next.RefreshToken);
        await RefreshAsync(provider, s11.RefreshToken);

        Assert.Equal(2, (await AuditRowsAsync(IdentityAuditActions.RefreshFamilyRevoked)).Count);
    }

    private async Task<(Guid SessionId, SignInResult Login)> SignInAsync(ServiceProvider provider)
    {
        await SeedUserAsync(provider, "tab.user", [RoleNames.TicketOperator]);
        var login = await LoginAsync(provider, "tab.user");
        Assert.True(login.IsSuccess);
        return (FakeAccessTokenIssuer.Parse(login.Value.AccessToken).SessionId, login.Value);
    }

    private async Task<Result<SignInResult>> RefreshAsync(ServiceProvider provider, string? refreshToken)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RefreshSessionHandler>()
            .Handle(new RefreshSessionCommand(refreshToken), CancellationToken);
    }

    private static MeterListener Listen(string instrument, Action<long> onMeasurement)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (published, meterListener) =>
            {
                if (published.Meter.Name == IdentityTelemetry.MeterName && published.Name == instrument)
                {
                    meterListener.EnableMeasurementEvents(published);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => onMeasurement(value));
        listener.Start();
        return listener;
    }
}
