using YCR.Domain.Identity;

namespace YCR.Domain.Tests.Identity;

/// <summary>
/// One AuthSession is one refresh-token family (D12). Rotation, the ~20 s predecessor grace and
/// family revocation (ADR-0016 §Refresh; spec R5–R8, S7, S9–S11, S13–S15), with no database.
/// </summary>
public sealed class AuthSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(20);
    private static readonly Guid SessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Token1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Token2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Token3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Token4 = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static readonly byte[] Hash1 = Hash(1);
    private static readonly byte[] Hash2 = Hash(2);
    private static readonly byte[] Hash3 = Hash(3);
    private static readonly byte[] Hash4 = Hash(4);

    [Fact]
    public void Start_SetsExpiryTwelveHoursAfterCreation()
    {
        var session = Start();

        Assert.Equal(SessionId, session.Id);
        Assert.Equal(UserId, session.UserId);
        Assert.Equal(Now, session.CreatedAtUtc);
        Assert.Equal(Now.AddHours(12), session.ExpiresAtUtc);
        Assert.Null(session.RevokedAtUtc);
        Assert.Null(session.RevocationReason);
        var token = Assert.Single(session.Tokens);
        Assert.Equal(Token1, token.Id);
        Assert.Equal(SessionId, token.SessionId);
        Assert.Equal(Hash1, token.TokenHash);
        Assert.Equal(Now, token.IssuedAtUtc);
        Assert.Null(token.RotatedAtUtc);
        Assert.Null(token.ReplacedByTokenId);
        Assert.True(session.IsActive(Now));
    }

    [Fact]
    public void Start_WithNonUtcTime_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 9, 23, 9, 30, 0, TimeSpan.FromHours(6.5));

        Assert.Throws<ArgumentException>(() => AuthSession.Start(SessionId, UserId, Token1, Hash1, nonUtc, Lifetime));
    }

    [Fact]
    public void Start_WithHashNot32Bytes_Throws()
    {
        Assert.Throws<ArgumentException>(() => AuthSession.Start(SessionId, UserId, Token1, new byte[31], Now, Lifetime));
    }

    [Fact]
    public void Rotate_CurrentToken_RotatesAndAddsSuccessor()
    {
        var session = Start();
        var at = Now.AddMinutes(10);

        var outcome = session.Rotate(Hash1, Token2, Hash2, at, Grace);

        Assert.Equal(RefreshOutcome.Rotated, outcome);
        var first = session.Tokens.Single(token => token.Id == Token1);
        Assert.Equal(at, first.RotatedAtUtc);
        Assert.Equal(Token2, first.ReplacedByTokenId);
        var successor = session.Tokens.Single(token => token.Id == Token2);
        Assert.Equal(Hash2, successor.TokenHash);
        Assert.Equal(at, successor.IssuedAtUtc);
        Assert.Null(successor.RotatedAtUtc);
        Assert.Equal(Now.AddHours(12), session.ExpiresAtUtc);
        Assert.Null(session.RevokedAtUtc);
    }

    [Fact]
    public void Rotate_ImmediatePredecessorWithinGrace_ReturnsSuperseded()
    {
        var session = Start();
        session.Rotate(Hash1, Token2, Hash2, Now, Grace);

        var outcome = session.Rotate(Hash1, Token3, Hash3, Now.AddSeconds(20), Grace);

        Assert.Equal(RefreshOutcome.Superseded, outcome);
        Assert.Equal(2, session.Tokens.Count);
        Assert.Null(session.RevokedAtUtc);
        Assert.Null(session.Tokens.Single(token => token.Id == Token2).RotatedAtUtc);
    }

    [Fact]
    public void Rotate_ImmediatePredecessorAfterGrace_RevokesFamily()
    {
        var session = Start();
        session.Rotate(Hash1, Token2, Hash2, Now, Grace);
        var at = Now.AddSeconds(20).AddTicks(1);

        var outcome = session.Rotate(Hash1, Token3, Hash3, at, Grace);

        Assert.Equal(RefreshOutcome.FamilyReused, outcome);
        Assert.Equal(at, session.RevokedAtUtc);
        Assert.Equal(RevocationReason.FamilyReuse, session.RevocationReason);
        Assert.Equal(2, session.Tokens.Count);
        Assert.False(session.IsActive(at));
    }

    [Fact]
    public void Rotate_OlderAncestor_RevokesFamilyEvenWithinGrace()
    {
        var session = Start();
        session.Rotate(Hash1, Token2, Hash2, Now, Grace);
        session.Rotate(Hash2, Token3, Hash3, Now.AddSeconds(1), Grace);

        var outcome = session.Rotate(Hash1, Token4, Hash4, Now.AddSeconds(2), Grace);

        Assert.Equal(RefreshOutcome.FamilyReused, outcome);
        Assert.Equal(RevocationReason.FamilyReuse, session.RevocationReason);
        Assert.Equal(3, session.Tokens.Count);
    }

    [Fact]
    public void Rotate_SuccessorAfterFamilyRevoked_ReturnsInvalid()
    {
        var session = Start();
        session.Rotate(Hash1, Token2, Hash2, Now, Grace);
        session.Rotate(Hash1, Token3, Hash3, Now.AddMinutes(1), Grace);

        var outcome = session.Rotate(Hash2, Token4, Hash4, Now.AddMinutes(2), Grace);

        Assert.Equal(RefreshOutcome.Invalid, outcome);
        Assert.Equal(2, session.Tokens.Count);
    }

    [Fact]
    public void Rotate_AfterAbsoluteLifetime_ReturnsInvalid()
    {
        var session = Start();

        var outcome = session.Rotate(Hash1, Token2, Hash2, Now.AddHours(12), Grace);

        Assert.Equal(RefreshOutcome.Invalid, outcome);
        Assert.Single(session.Tokens);
        Assert.Null(session.RevokedAtUtc);
    }

    [Fact]
    public void Rotate_OnRevokedSession_ReturnsInvalid()
    {
        var session = Start();
        session.Revoke(RevocationReason.Logout, Now);

        var outcome = session.Rotate(Hash1, Token2, Hash2, Now.AddMinutes(1), Grace);

        Assert.Equal(RefreshOutcome.Invalid, outcome);
        Assert.Single(session.Tokens);
        Assert.Equal(RevocationReason.Logout, session.RevocationReason);
    }

    [Fact]
    public void Rotate_UnknownHash_ReturnsInvalid()
    {
        var session = Start();

        var outcome = session.Rotate(Hash4, Token2, Hash2, Now, Grace);

        Assert.Equal(RefreshOutcome.Invalid, outcome);
        Assert.Single(session.Tokens);
        Assert.Null(session.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_WhenActive_SetsRevokedAtAndReason()
    {
        var session = Start();

        var changed = session.Revoke(RevocationReason.AdministratorRevoked, Now.AddMinutes(5));

        Assert.True(changed);
        Assert.Equal(Now.AddMinutes(5), session.RevokedAtUtc);
        Assert.Equal(RevocationReason.AdministratorRevoked, session.RevocationReason);
        Assert.False(session.IsActive(Now.AddMinutes(5)));
    }

    [Fact]
    public void Revoke_WhenRevoked_ReportsNoChange()
    {
        var session = Start();
        session.Revoke(RevocationReason.Logout, Now);

        var changed = session.Revoke(RevocationReason.UserDisabled, Now.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(Now, session.RevokedAtUtc);
        Assert.Equal(RevocationReason.Logout, session.RevocationReason);
    }

    [Fact]
    public void IsActive_AtExpiry_ReturnsFalse()
    {
        var session = Start();

        Assert.True(session.IsActive(Now.AddHours(12).AddTicks(-1)));
        Assert.False(session.IsActive(Now.AddHours(12)));
    }

    private static AuthSession Start() =>
        AuthSession.Start(SessionId, UserId, Token1, Hash1, Now, Lifetime);

    private static byte[] Hash(byte seed) => Enumerable.Repeat(seed, 32).ToArray();
}
