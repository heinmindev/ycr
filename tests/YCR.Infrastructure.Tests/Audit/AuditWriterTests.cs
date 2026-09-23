using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Identity;
using YCR.Infrastructure.Persistence;
using YCR.Infrastructure.Tests.Identity;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Audit;

/// <summary>
/// F-002 plan P5 (spec R12, U4, S27): the two actor paths for events that have no bearer
/// principal, and <c>ActorRole</c> as a JSON array of canonical role identifiers.
/// </summary>
/// <remarks>
/// Every test sets an <em>ambient</em> principal naming someone else, standing in for a bearer
/// token attached to <c>/auth/login</c> or <c>/auth/refresh</c>. It must reach no actor field.
/// Credential: <c>ycr_app</c>.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class AuditWriterTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid AmbientUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly StubCurrentUser ambient = new()
    {
        UserId = AmbientUserId,
        Roles = [RoleNames.SystemAdministrator],
        ClientIp = "198.51.100.23",
        CorrelationId = "corr-audit-writer",
        AuthorizedByPermission = "users.manage",
    };

    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("audit_writer", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RecordWithoutActor_IgnoresAmbientPrincipal()
    {
        var subjectId = Guid.NewGuid();

        await WriteAsync(writer => writer.RecordWithoutActor(
            "Identity.LoginFailed", "Identity.User", subjectId, before: null, after: null));

        var row = await ReadSingleRowAsync();
        Assert.Equal("Identity.LoginFailed", row.Action);
        Assert.Null(row.ActorUserId);
        Assert.Null(row.ActorRole);
        Assert.Null(row.AuthorizedByPermission);
        Assert.Equal(subjectId, row.SubjectId);
        // Address and correlation are the server's view of the request, not an actor claim.
        Assert.Equal("198.51.100.23", row.ClientIp);
        Assert.Equal("corr-audit-writer", row.CorrelationId);
    }

    [Fact]
    public async Task RecordSignIn_UsesVerifiedUserAndRoles()
    {
        var user = StaffUser.Create(Guid.NewGuid(), UserName.Create("signed.in").Value, DateTimeOffset.UnixEpoch);

        await WriteAsync(writer => writer.RecordSignIn(
            user.Id,
            [RoleNames.StationManager, RoleNames.TicketOperator],
            "Identity.LoginSucceeded",
            "Identity.User",
            after: null));

        var row = await ReadSingleRowAsync();
        Assert.Equal(user.Id, row.ActorUserId);
        Assert.NotEqual(AmbientUserId, row.ActorUserId);
        Assert.Equal("""["StationManager","TicketOperator"]""", row.ActorRole);
        Assert.Null(row.AuthorizedByPermission);
        Assert.Equal(user.Id, row.SubjectId);
    }

    [Fact]
    public async Task RecordSignIn_WithNoRoles_WritesNullActorRole()
    {
        var user = StaffUser.Create(Guid.NewGuid(), UserName.Create("no.roles").Value, DateTimeOffset.UnixEpoch);

        await WriteAsync(writer => writer.RecordSignIn(user.Id, [], "Identity.LoginSucceeded", "Identity.User", after: null));

        Assert.Null((await ReadSingleRowAsync()).ActorRole);
    }

    [Fact]
    public async Task Record_ActorRoleIsJsonArrayOfCanonicalIdentifiers()
    {
        await WriteAsync(writer => writer.Record("Identity.UserDisabled", "Identity.User", Guid.NewGuid(), before: null, after: null));

        var row = await ReadSingleRowAsync();
        Assert.Equal(AmbientUserId, row.ActorUserId);
        Assert.Equal("""["SystemAdministrator"]""", row.ActorRole);
        Assert.Equal("users.manage", row.AuthorizedByPermission);
    }

    private async Task WriteAsync(Action<IAuditWriter> write)
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(database.ApplicationConnectionString)
            .AddScoped<ICurrentUser>(_ => ambient)
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        write(scope.ServiceProvider.GetRequiredService<IAuditWriter>());
        await scope.ServiceProvider.GetRequiredService<YcrDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AuditRow> ReadSingleRowAsync()
    {
        var rows = await IdentitySql.PairsAsync(
            database.MigratorConnectionString,
            """
            SELECT [Action],
                   CONCAT(ISNULL(CONVERT(nvarchar(36), [ActorUserId]), N'-'), N'|', ISNULL([ActorRole], N'-'), N'|',
                          ISNULL([AuthorizedByPermission], N'-'), N'|', ISNULL(CONVERT(nvarchar(36), [SubjectId]), N'-'), N'|',
                          ISNULL([ClientIp], N'-'), N'|', [CorrelationId])
            FROM [audit].[AuditEvents];
            """);
        var (action, packed) = Assert.Single(rows);
        var fields = packed.Split('|');
        return new AuditRow(
            action,
            fields[0] == "-" ? null : Guid.Parse(fields[0]),
            fields[1] == "-" ? null : fields[1],
            fields[2] == "-" ? null : fields[2],
            fields[3] == "-" ? null : Guid.Parse(fields[3]),
            fields[4] == "-" ? null : fields[4],
            fields[5]);
    }

    private sealed record AuditRow(
        string Action,
        Guid? ActorUserId,
        string? ActorRole,
        string? AuthorizedByPermission,
        Guid? SubjectId,
        string? ClientIp,
        string CorrelationId);

    private sealed class StubCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; init; }

        public IReadOnlyCollection<string> Roles { get; init; } = [];

        public string? ClientIp { get; init; }

        public required string CorrelationId { get; init; }

        public string? AuthorizedByPermission { get; init; }
    }
}
