using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using YCR.Application.Timetable;
using YCR.Domain.Common;
using YCR.Domain.Timetable;
using YCR.Infrastructure.Persistence;
using YCR.Infrastructure.Persistence.Migrations;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-005 spec §7 and plan §DB changes 1: the timetable-version tables as the EF model declares
/// them, and the <c>Timetable_CreateScheduleVersions</c> migration EF generated from it.
/// </summary>
/// <remarks>
/// Asserted against the design-time model, the one <c>has-pending-model-changes</c> diffs; the
/// runtime model drops check constraints.
/// </remarks>
public sealed class ScheduleModelTests
{
    [Fact]
    public void ScheduleModel_MapsTablesColumnsTypesAndKeys()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var versions = model.FindEntityType(typeof(ScheduleVersion))!;
        Assert.Equal("ScheduleVersions", versions.GetTableName());
        Assert.Equal("timetable", versions.GetSchema());
        Assert.Equal("PK_ScheduleVersions", versions.FindPrimaryKey()!.GetName());
        Assert.Equal([nameof(ScheduleVersion.Id)], versions.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(ValueGenerated.Never, versions.FindProperty(nameof(ScheduleVersion.Id))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, versions.FindProperty(nameof(ScheduleVersion.Number))!.ValueGenerated);
        Assert.Equal(
            [
                "CancelledAtUtc:datetimeoffset(3):True",
                "CreatedAtUtc:datetimeoffset(3):False",
                "DiscardedAtUtc:datetimeoffset(3):True",
                "EffectiveFrom:date:False",
                "Id:uniqueidentifier:False",
                "NameEn:nvarchar(100):False",
                "NameMy:nvarchar(100):False",
                "Number:int:False",
                "PublishedAtUtc:datetimeoffset(3):True",
                "Status:nvarchar(10):False",
            ],
            ColumnsOf(model, StoreObjectIdentifier.Table("ScheduleVersions", "timetable")));

        // R47, E7: Status is the only concurrency token; no rowversion, no client-held version.
        Assert.Equal(
            [nameof(ScheduleVersion.Status)],
            versions.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => p.Name));
        Assert.DoesNotContain(
            new[] { typeof(ScheduleVersion), typeof(ScheduleVersionService), typeof(ScheduleStopTime) }
                .SelectMany(type => model.FindEntityType(type)!.GetProperties()),
            p => p.GetColumnType() == "rowversion");
        Assert.DoesNotContain(versions.GetProperties(), p => p.Name == nameof(AggregateRoot.DomainEvents));
        Assert.Equal(typeof(string), versions.FindProperty(nameof(ScheduleVersion.Status))!.GetProviderClrType());

        var entries = model.FindEntityType(typeof(ScheduleVersionService))!;
        Assert.Equal("ScheduleVersionServices", entries.GetTableName());
        Assert.Equal("timetable", entries.GetSchema());
        Assert.Equal("PK_ScheduleVersionServices", entries.FindPrimaryKey()!.GetName());
        Assert.Equal(
            [nameof(ScheduleVersionService.ScheduleVersionId), nameof(ScheduleVersionService.ServiceId)],
            entries.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(
            ["ScheduleVersionId:uniqueidentifier:False", "ServiceId:uniqueidentifier:False"],
            ColumnsOf(model, StoreObjectIdentifier.Table("ScheduleVersionServices", "timetable")));

        var stopTimes = model.FindEntityType(typeof(ScheduleStopTime))!;
        Assert.Equal("ScheduleStopTimes", stopTimes.GetTableName());
        Assert.Equal("timetable", stopTimes.GetSchema());
        Assert.Equal("PK_ScheduleStopTimes", stopTimes.FindPrimaryKey()!.GetName());
        Assert.Equal(
            [nameof(ScheduleStopTime.ScheduleVersionId), nameof(ScheduleStopTime.ServiceId), nameof(ScheduleStopTime.Position)],
            stopTimes.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(
            [
                "ArrivalMinute:smallint:True",
                "DepartureMinute:smallint:True",
                "Position:int:False",
                "ScheduleVersionId:uniqueidentifier:False",
                "ServiceId:uniqueidentifier:False",
            ],
            ColumnsOf(model, StoreObjectIdentifier.Table("ScheduleStopTimes", "timetable")));

        // V7: the ADR-0027 times are stored as smallint minutes through a converter.
        Assert.Equal(typeof(short), stopTimes.FindProperty(nameof(ScheduleStopTime.Arrival))!.GetValueConverter()!.ProviderClrType);
        Assert.Equal(typeof(short), stopTimes.FindProperty(nameof(ScheduleStopTime.Departure))!.GetValueConverter()!.ProviderClrType);

        Assert.DoesNotContain(entries.GetProperties().Concat(stopTimes.GetProperties()), p => p.IsConcurrencyToken);
    }

    /// <summary>
    /// P21 and O1: each table has exactly the indexes spec §7 and the plan approved, all declared
    /// and named, with the filter and uniqueness pinned; and the migration creates exactly these.
    /// </summary>
    [Fact]
    public void ScheduleModel_HasExactlyTheDeclaredIndexes()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        Assert.Equal(
            [
                "UX_ScheduleVersions_EffectiveFrom_Published:EffectiveFrom:True:[Status] = N'Published'",
                "UX_ScheduleVersions_Number:Number:True:",
            ],
            IndexesOf(model.FindEntityType(typeof(ScheduleVersion))!));
        Assert.Equal(
            ["IX_ScheduleVersionServices_ServiceId:ServiceId:False:"],
            IndexesOf(model.FindEntityType(typeof(ScheduleVersionService))!));
        Assert.Equal(
            ["IX_ScheduleStopTimes_ServiceId_Position:ServiceId,Position:False:"],
            IndexesOf(model.FindEntityType(typeof(ScheduleStopTime))!));

        var created = new ExposedTimetableCreateScheduleVersions().GetUpOperations()
            .OfType<CreateIndexOperation>()
            .Select(index => $"{index.Schema}.{index.Table}.{index.Name}:{string.Join(",", index.Columns)}:{index.IsUnique}:{index.Filter}")
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "timetable.ScheduleStopTimes.IX_ScheduleStopTimes_ServiceId_Position:ServiceId,Position:False:",
                "timetable.ScheduleVersionServices.IX_ScheduleVersionServices_ServiceId:ServiceId:False:",
                "timetable.ScheduleVersions.UX_ScheduleVersions_EffectiveFrom_Published:EffectiveFrom:True:[Status] = N'Published'",
                "timetable.ScheduleVersions.UX_ScheduleVersions_Number:Number:True:",
            ],
            created);
    }

    /// <summary>
    /// Spec §7, E2: four keys, all <c>NO ACTION</c> and inside <c>timetable</c>; the two into F-004's
    /// tables have no navigation in either direction, and the two owned ones only the parent's.
    /// </summary>
    [Fact]
    public void ScheduleModel_ForeignKeys_AreNoActionWithinTimetableWithoutNavigationsToService()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var entryKeys = model.FindEntityType(typeof(ScheduleVersionService))!.GetForeignKeys()
            .OrderBy(fk => fk.GetConstraintName(), StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            ["FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId", "FK_ScheduleVersionServices_Services_ServiceId"],
            entryKeys.Select(fk => fk.GetConstraintName()));
        Assert.Equal(typeof(ScheduleVersion), entryKeys[0].PrincipalEntityType.ClrType);
        Assert.Equal(nameof(ScheduleVersion.Services), entryKeys[0].PrincipalToDependent?.Name);
        Assert.Null(entryKeys[0].DependentToPrincipal);
        Assert.Equal(typeof(Service), entryKeys[1].PrincipalEntityType.ClrType);
        Assert.Equal([nameof(ScheduleVersionService.ServiceId)], entryKeys[1].Properties.Select(p => p.Name));
        Assert.Null(entryKeys[1].DependentToPrincipal);
        Assert.Null(entryKeys[1].PrincipalToDependent);

        var stopTimeKeys = model.FindEntityType(typeof(ScheduleStopTime))!.GetForeignKeys()
            .OrderBy(fk => fk.GetConstraintName(), StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            ["FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId", "FK_ScheduleStopTimes_ServiceStops_ServiceId_Position"],
            stopTimeKeys.Select(fk => fk.GetConstraintName()));
        Assert.Equal(typeof(ScheduleVersionService), stopTimeKeys[0].PrincipalEntityType.ClrType);
        Assert.Equal(
            [nameof(ScheduleStopTime.ScheduleVersionId), nameof(ScheduleStopTime.ServiceId)],
            stopTimeKeys[0].Properties.Select(p => p.Name));
        Assert.Equal(nameof(ScheduleVersionService.StopTimes), stopTimeKeys[0].PrincipalToDependent?.Name);
        Assert.Null(stopTimeKeys[0].DependentToPrincipal);
        Assert.Equal(typeof(ServiceStop), stopTimeKeys[1].PrincipalEntityType.ClrType);
        Assert.Equal("PK_ServiceStops", stopTimeKeys[1].PrincipalKey.GetName());
        Assert.Equal([nameof(ScheduleStopTime.ServiceId), nameof(ScheduleStopTime.Position)], stopTimeKeys[1].Properties.Select(p => p.Name));
        Assert.Null(stopTimeKeys[1].DependentToPrincipal);
        Assert.Null(stopTimeKeys[1].PrincipalToDependent);

        Assert.All(entryKeys.Concat(stopTimeKeys), fk =>
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
            Assert.Equal("timetable", fk.PrincipalEntityType.GetSchema());
        });

        // F-004's entities gained no navigation to a schedule type.
        Assert.DoesNotContain(
            model.FindEntityType(typeof(Service))!.GetNavigations()
                .Concat(model.FindEntityType(typeof(ServiceStop))!.GetNavigations()),
            navigation => navigation.TargetEntityType.ClrType.Name.StartsWith("Schedule", StringComparison.Ordinal));

        // The migration agrees: four keys, all NO ACTION, all to timetable.
        var tables = new ExposedTimetableCreateScheduleVersions().GetUpOperations().OfType<CreateTableOperation>().ToList();
        Assert.Equal(
            [
                "FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId:ScheduleVersionId,ServiceId:timetable.ScheduleVersionServices:ScheduleVersionId,ServiceId:NoAction",
                "FK_ScheduleStopTimes_ServiceStops_ServiceId_Position:ServiceId,Position:timetable.ServiceStops:ServiceId,Position:NoAction",
                "FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId:ScheduleVersionId:timetable.ScheduleVersions:Id:NoAction",
                "FK_ScheduleVersionServices_Services_ServiceId:ServiceId:timetable.Services:Id:NoAction",
            ],
            tables.SelectMany(table => table.ForeignKeys)
                .Select(fk => $"{fk.Name}:{string.Join(",", fk.Columns)}:{fk.PrincipalSchema}.{fk.PrincipalTable}:{string.Join(",", fk.PrincipalColumns!)}:{fk.OnDelete}")
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ScheduleModel_DeclaresEveryCheckConstraint()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        Assert.Equal(
            [
                "CK_ScheduleVersions_CancelledAtUtc_Utc=[CancelledAtUtc] IS NULL OR DATEPART(TZOFFSET, [CancelledAtUtc]) = 0",
                "CK_ScheduleVersions_CreatedAtUtc_Utc=DATEPART(TZOFFSET, [CreatedAtUtc]) = 0",
                "CK_ScheduleVersions_DiscardedAtUtc_Utc=[DiscardedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DiscardedAtUtc]) = 0",
                "CK_ScheduleVersions_Number=[Number] >= 1",
                "CK_ScheduleVersions_PublishedAtUtc_Utc=[PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0",
                "CK_ScheduleVersions_Status=[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')",
                "CK_ScheduleVersions_StatusInstants=([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Discarded' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Cancelled' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)",
            ],
            ChecksOf(model, typeof(ScheduleVersion)));

        Assert.Empty(ChecksOf(model, typeof(ScheduleVersionService)));

        Assert.Equal(
            [
                "CK_ScheduleStopTimes_AnyTime=[ArrivalMinute] IS NOT NULL OR [DepartureMinute] IS NOT NULL",
                "CK_ScheduleStopTimes_Dwell=[ArrivalMinute] IS NULL OR [DepartureMinute] IS NULL OR [DepartureMinute] >= [ArrivalMinute]",
                "CK_ScheduleStopTimes_Minutes=([ArrivalMinute] IS NULL OR [ArrivalMinute] BETWEEN 0 AND 1439) AND ([DepartureMinute] IS NULL OR [DepartureMinute] BETWEEN 0 AND 1439)",
            ],
            ChecksOf(model, typeof(ScheduleStopTime)));

        // The migration creates each check with its table.
        var tables = new ExposedTimetableCreateScheduleVersions().GetUpOperations().OfType<CreateTableOperation>().ToList();
        Assert.Equal(
            ChecksOf(model, typeof(ScheduleVersion)).Concat(ChecksOf(model, typeof(ScheduleStopTime))).Order(StringComparer.Ordinal),
            tables.SelectMany(table => table.CheckConstraints).Select(check => $"{check.Name}={check.Sql}").Order(StringComparer.Ordinal));
    }

    /// <summary>P19: the name Application matches on is the name the model maps.</summary>
    [Fact]
    public void ScheduleModel_ConstraintNames_MatchTheModel()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var index = model.FindEntityType(typeof(ScheduleVersion))!.GetIndexes()
            .Single(candidate => candidate.Properties.Select(p => p.Name).SequenceEqual([nameof(ScheduleVersion.EffectiveFrom)]));
        Assert.Equal(TimetableConstraints.ScheduleVersionEffectiveFromPublishedUniqueIndex, index.GetDatabaseName());
        Assert.Equal("UX_ScheduleVersions_EffectiveFrom_Published", TimetableConstraints.ScheduleVersionEffectiveFromPublishedUniqueIndex);
    }

    /// <summary>
    /// O2: the migration creates exactly the three tables and four indexes, all in <c>timetable</c>;
    /// nothing against <c>Services</c>, <c>ServiceStops</c> or <c>network</c>; no raw SQL; and it
    /// does not ensure the schema, which F-004's migration owns.
    /// </summary>
    [Fact]
    public void UpMigration_TimetableCreateScheduleVersions_CreatesOnlyTheThreeTablesAndFourIndexes()
    {
        var operations = new ExposedTimetableCreateScheduleVersions().GetUpOperations();

        Assert.Equal(
            [
                "CreateTableOperation:timetable.ScheduleVersions",
                "CreateTableOperation:timetable.ScheduleVersionServices",
                "CreateTableOperation:timetable.ScheduleStopTimes",
                "CreateIndexOperation:timetable.ScheduleStopTimes",
                "CreateIndexOperation:timetable.ScheduleVersions",
                "CreateIndexOperation:timetable.ScheduleVersions",
                "CreateIndexOperation:timetable.ScheduleVersionServices",
            ],
            operations.Select(Describe));
        Assert.DoesNotContain(operations, operation => operation is SqlOperation or EnsureSchemaOperation or AlterTableOperation);
    }

    /// <summary>Rollback: the three tables are dropped, children first; the schema stays (F-004 owns it).</summary>
    [Fact]
    public void DownMigration_TimetableCreateScheduleVersions_DropsTheThreeTablesAndKeepsTheSchema()
    {
        var operations = new ExposedTimetableCreateScheduleVersions().GetDownOperations();

        Assert.Equal(
            [
                "DropTableOperation:timetable.ScheduleStopTimes",
                "DropTableOperation:timetable.ScheduleVersionServices",
                "DropTableOperation:timetable.ScheduleVersions",
            ],
            operations.Select(Describe));
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        CreateTableOperation table => $"{nameof(CreateTableOperation)}:{table.Schema}.{table.Name}",
        DropTableOperation table => $"{nameof(DropTableOperation)}:{table.Schema}.{table.Name}",
        CreateIndexOperation index => $"{nameof(CreateIndexOperation)}:{index.Schema}.{index.Table}",
        _ => operation.GetType().Name
    };

    private static List<string> ColumnsOf(IModel model, StoreObjectIdentifier table) =>
        [.. model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.GetColumnName(table) is not null)
            .Select(property => $"{property.GetColumnName(table)}:{property.GetColumnType(table)}:{property.IsColumnNullable(table)}")
            .Distinct()
            .Order(StringComparer.Ordinal)];

    private static List<string> IndexesOf(IEntityType entity) =>
        [.. entity.GetIndexes()
            .Select(index => $"{index.GetDatabaseName()}:{string.Join(",", index.Properties.Select(p => p.Name))}:{index.IsUnique}:{index.GetFilter()}")
            .Order(StringComparer.Ordinal)];

    private static List<string> ChecksOf(IModel model, Type entity) =>
        [.. model.FindEntityType(entity)!.GetCheckConstraints()
            .Select(check => $"{check.Name}={check.Sql}")
            .Order(StringComparer.Ordinal)];

    private static YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options);

    private static IModel DesignTimeModel(YcrDbContext context) => context.GetService<IDesignTimeModel>().Model;

    private sealed class ExposedTimetableCreateScheduleVersions : Timetable_CreateScheduleVersions
    {
        public IReadOnlyList<MigrationOperation> GetUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> GetDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
