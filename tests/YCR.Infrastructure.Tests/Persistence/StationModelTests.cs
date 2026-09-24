using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using YCR.Application.Network;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Infrastructure.Identifiers;
using YCR.Infrastructure.Persistence;
using YCR.Infrastructure.Persistence.Migrations;

namespace YCR.Infrastructure.Tests.Persistence;

public sealed class StationModelTests
{
    [Fact]
    public void Model_WithStationEntity_MapsTableAndCodeIndex()
    {
        var options = new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options;

        using var context = new YcrDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Station));

        Assert.NotNull(entity);
        Assert.Equal("Stations", entity.GetTableName());
        Assert.Equal("network", entity.GetSchema());

        var codeIndex = Assert.Single(entity.GetIndexes(), index =>
            index.Properties.Count == 1 && index.Properties[0].Name == nameof(Station.Code));
        Assert.True(codeIndex.IsUnique);
        // Compared against the Application constant, not a literal. CreateStationHandler matches
        // on that constant to turn a duplicate code into a 409; a rename here without one there
        // would turn it into an unhandled 500, and nothing would fail until a duplicate code was
        // posted in production.
        Assert.Equal(NetworkConstraints.StationCodeUniqueIndex, codeIndex.GetDatabaseName());
        Assert.Equal("UX_Stations_Code", NetworkConstraints.StationCodeUniqueIndex);

        var isActive = entity.FindProperty(nameof(Station.IsActive));
        Assert.NotNull(isActive);
        Assert.True(isActive.IsConcurrencyToken);
    }

    /// <summary>
    /// R-4 (hein's ruling, 2026-09-22): both UTC check constraints are declared in the EF model,
    /// not only in a migration, so `dotnet ef migrations has-pending-model-changes` can detect
    /// drift. The CI step that runs that command is the other half of this control.
    /// </summary>
    /// <remarks>
    /// `CK_AuditEvents_OccurredAtUtc_Utc` is in the model even though `AuditEvent` is mapped with
    /// `ExcludeFromMigrations()` — that switch stops EF *generating* DDL for the ledger table
    /// (ADR-0017 item 6), it does not stop EF knowing the shape. The raw SQL in
    /// `Audit_CreateAuditEventsLedger` is what creates it; this assertion is what notices if the
    /// two ever disagree.
    /// </remarks>
    [Theory]
    [InlineData(
        "YCR.Domain.Network.Station",
        "CK_Stations_CreatedAtUtc_Utc",
        "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0")]
    [InlineData(
        "YCR.Domain.Network.Route",
        "CK_Routes_CreatedAtUtc_Utc",
        "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0")]
    [InlineData(
        "YCR.Domain.Network.Route",
        "CK_Routes_DeactivatedAtUtc_Utc",
        "[DeactivatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0")]
    // Named rather than typed: AuditEvent is internal to YCR.Infrastructure on purpose, so that
    // nothing above Infrastructure can construct an audit row outside IAuditWriter. Reaching it
    // through the model keeps that closed.
    [InlineData(
        "YCR.Infrastructure.Audit.AuditEvent",
        "CK_AuditEvents_OccurredAtUtc_Utc",
        "DATEPART(TZOFFSET, [OccurredAtUtc]) = 0")]
    public void Model_WithUtcColumn_DeclaresItsCheckConstraint(
        string entityTypeName,
        string constraintName,
        string sql)
    {
        var options = new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options;

        using var context = new YcrDbContext(options);

        // The design-time model, not context.Model: EF strips check constraints out of the
        // read-optimized runtime model, because nothing at runtime consults them. The design-time
        // model is the one `has-pending-model-changes` diffs, so it is the one worth asserting.
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(entityTypeName);

        Assert.NotNull(entity);
        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate => candidate.Name == constraintName);
        Assert.Equal(sql, constraint.Sql);
    }

    [Fact]
    public void Model_WithDomainEvents_IgnoresDomainEventsExplicitly()
    {
        var options = new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options;

        using var context = new YcrDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Station));

        Assert.NotNull(entity);
        Assert.DoesNotContain(
            entity.GetProperties(),
            property => property.Name == nameof(AggregateRoot.DomainEvents));
        Assert.DoesNotContain(
            entity.GetNavigations(),
            navigation => navigation.Name == nameof(AggregateRoot.DomainEvents));
    }

    [Fact]
    public void SequentialGuidGenerator_ReturnsDistinctNonEmptyIds()
    {
        var generator = new SqlServerSequentialGuidIdGenerator();

        var first = generator.New();
        var second = generator.New();

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(Guid.Empty, second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DownMigration_WithStationsTable_DropsNetworkSchema()
    {
        var operations = new ExposedNetworkCreateStations().GetDownOperations();

        Assert.Contains(
            operations,
            operation => operation is DropSchemaOperation { Name: "network" });
    }

    private sealed class ExposedNetworkCreateStations : Network_CreateStations
    {
        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
