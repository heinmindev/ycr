using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;
using YCR.TestSupport;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// The tech-lead ruling of 2026-09-20: Infrastructure turns SQL Server errors 2601 and 2627 into
/// <see cref="UniqueConstraintViolationException"/>, carrying the constraint name, so Application
/// can recognise a specific conflict without referencing the provider (ADR-0004).
/// </summary>
/// <remarks>
/// Run against real SQL Server on purpose. The constraint name has to be parsed out of the
/// server's message text, so a test over a hand-built exception would prove only that the regex
/// matches a string this file wrote. Both error numbers are provoked from actual DDL.
/// <para>
/// Credential: <c>ycr_app</c> for the EF path, because that is the identity that will hit this
/// in production; the migrator only for the extra DDL that provokes error 2627.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class UniqueConstraintTranslationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private TestDatabase database = null!;

    public async ValueTask InitializeAsync() =>
        database = await fixture.Container.ProvisionDatabaseAsync("unique_translation", TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Error 2601 — the station code unique index, which is the case F-001 actually hits.</summary>
    [Fact]
    public async Task SaveChanges_WithDuplicateStationCode_ThrowsUniqueConstraintViolationNamingTheIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var first = NewContext())
        {
            first.Stations.Add(NewStation("BGO"));
            await first.SaveChangesAsync(cancellationToken);
        }

        await using var second = NewContext();
        second.Stations.Add(NewStation("BGO"));

        var failure = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => second.SaveChangesAsync(cancellationToken));

        // CreateStationHandler matches on this name and lets anything else propagate, so the
        // name being exactly right is what keeps an unrelated conflict from being reported as a
        // duplicate station code.
        Assert.Equal("UX_Stations_Code", failure.ConstraintName);
        Assert.IsType<DbUpdateException>(failure.InnerException);
    }

    /// <summary>
    /// Error 2627 — a named UNIQUE KEY constraint. F-001 has none, so one is created here: the
    /// translator claims to handle both numbers and the second claim would otherwise be untested.
    /// </summary>
    [Fact]
    public async Task SaveChanges_WithUniqueKeyConstraintViolation_ThrowsUniqueConstraintViolationNamingTheConstraint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await ExecuteAsMigratorAsync(
            "ALTER TABLE [network].[Stations] ADD CONSTRAINT [UQ_Stations_NameEn] UNIQUE ([NameEn]);",
            cancellationToken);

        await using (var first = NewContext())
        {
            first.Stations.Add(NewStation("AAA", "Duplicated Name"));
            await first.SaveChangesAsync(cancellationToken);
        }

        await using var second = NewContext();
        second.Stations.Add(NewStation("BBB", "Duplicated Name"));

        var failure = await Assert.ThrowsAsync<UniqueConstraintViolationException>(
            () => second.SaveChangesAsync(cancellationToken));

        Assert.Equal("UQ_Stations_NameEn", failure.ConstraintName);
    }

    [Fact]
    public async Task SaveChanges_WithAnUnrelatedFailure_IsNotTranslated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // A check-constraint violation is not a uniqueness failure, so it must reach the caller
        // as the infrastructure error it is rather than being dressed up as a conflict.
        await ExecuteAsMigratorAsync(
            "ALTER TABLE [network].[Stations] ADD CONSTRAINT [CK_Stations_NameEn] CHECK ([NameEn] <> N'Forbidden');",
            cancellationToken);

        await using var context = NewContext();
        context.Stations.Add(NewStation("CCC", "Forbidden"));

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));

        Assert.IsNotType<UniqueConstraintViolationException>(failure);
    }

    private static Station NewStation(string code, string nameEn = "Bago") =>
        Station.Create(
            Guid.CreateVersion7(),
            StationCode.Create(code).Value,
            BilingualName.Create(nameEn, "ပဲခူး").Value,
            DateTimeOffset.UtcNow);

    private YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer(database.ApplicationConnectionString)
            .Options);

    private async Task ExecuteAsMigratorAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(database.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
