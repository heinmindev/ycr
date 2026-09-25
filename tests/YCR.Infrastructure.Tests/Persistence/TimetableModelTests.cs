using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Domain.Timetable;
using YCR.Infrastructure.Persistence;
using YCR.Infrastructure.Persistence.Migrations;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-004 spec §7 and plan §DB changes: the timetable tables as the EF model declares them, and the
/// <c>Timetable_CreateServices</c> migration EF generated from it.
/// </summary>
/// <remarks>
/// Asserted against the design-time model, the one <c>has-pending-model-changes</c> diffs; the
/// runtime model drops check constraints.
/// </remarks>
public sealed class TimetableModelTests
{
    [Fact]
    public void TimetableModel_MapsTablesColumnsTypesAndKeys()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var services = model.FindEntityType(typeof(Service))!;
        Assert.Equal("Services", services.GetTableName());
        Assert.Equal("timetable", services.GetSchema());
        Assert.Equal("PK_Services", services.FindPrimaryKey()!.GetName());
        Assert.Equal([nameof(Service.Id)], services.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(ValueGenerated.Never, services.FindProperty(nameof(Service.Id))!.ValueGenerated);

        var table = StoreObjectIdentifier.Table("Services", "timetable");
        Assert.Equal(
            [
                "Code:nvarchar(10):False",
                "CreatedAtUtc:datetimeoffset(3):False",
                "Direction:nvarchar(10):False",
                "EffectiveFrom:date:False",
                "EffectiveTo:date:True",
                "Id:uniqueidentifier:False",
                "NameEn:nvarchar(100):False",
                "NameMy:nvarchar(100):False",
                "RouteId:uniqueidentifier:False",
                "RunsOnFriday:bit:False",
                "RunsOnMonday:bit:False",
                "RunsOnSaturday:bit:False",
                "RunsOnSunday:bit:False",
                "RunsOnThursday:bit:False",
                "RunsOnTuesday:bit:False",
                "RunsOnWednesday:bit:False",
                "WithdrawnAtUtc:datetimeoffset(3):True",
            ],
            ColumnsOf(model, table));

        // R36 / E10: EffectiveTo is the only concurrency token, and there is no rowversion.
        Assert.Equal(
            [nameof(Service.EffectiveTo)],
            services.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => p.Name));
        Assert.DoesNotContain(services.GetProperties(), p => p.GetColumnType() == "rowversion");
        Assert.DoesNotContain(services.GetProperties(), p => p.Name is nameof(AggregateRoot.DomainEvents) or nameof(Service.NeverRuns));

        // P8: the direction is stored as its name.
        Assert.Equal(typeof(string), services.FindProperty(nameof(Service.Direction))!.GetProviderClrType());

        var stops = model.FindEntityType(typeof(ServiceStop))!;
        Assert.Equal("ServiceStops", stops.GetTableName());
        Assert.Equal("timetable", stops.GetSchema());
        Assert.Equal("PK_ServiceStops", stops.FindPrimaryKey()!.GetName());
        Assert.Equal(
            [nameof(ServiceStop.ServiceId), nameof(ServiceStop.Position)],
            stops.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(
            ["Position:int:False", "ServiceId:uniqueidentifier:False", "StationId:uniqueidentifier:False"],
            ColumnsOf(model, StoreObjectIdentifier.Table("ServiceStops", "timetable")));
        Assert.DoesNotContain(stops.GetProperties(), p => p.IsConcurrencyToken);
    }

    /// <summary>
    /// P16 and the F-003 IX lesson: each table has exactly the indexes spec §7 approved, all
    /// declared and named, and none unique (R35; the closure repeats a station, R14).
    /// </summary>
    [Fact]
    public void TimetableModel_HasExactlyTheDeclaredIndexes()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var services = model.FindEntityType(typeof(Service))!;
        Assert.Equal(
            ["IX_Services_Code_EffectiveFrom:Code,EffectiveFrom:False", "IX_Services_RouteId:RouteId:False"],
            IndexesOf(services));

        var stops = model.FindEntityType(typeof(ServiceStop))!;
        Assert.Equal(["IX_ServiceStops_StationId:StationId:False"], IndexesOf(stops));

        // The migration creates exactly these indexes, and no IX_ServiceStops_ServiceId: the
        // primary key (ServiceId, Position) already covers that foreign key (plan O2).
        var created = new ExposedTimetableCreateServices().GetUpOperations()
            .OfType<CreateIndexOperation>()
            .Select(index => $"{index.Table}.{index.Name}:{string.Join(",", index.Columns)}:{index.IsUnique}")
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                "ServiceStops.IX_ServiceStops_StationId:StationId:False",
                "Services.IX_Services_Code_EffectiveFrom:Code,EffectiveFrom:False",
                "Services.IX_Services_RouteId:RouteId:False",
            ],
            created);
    }

    /// <summary>R29, ADR-0025 items 5-7: both cross-schema keys are NO ACTION with no navigation.</summary>
    [Fact]
    public void TimetableModel_ForeignKeys_AreNoActionAcrossSchemasWithoutNavigations()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var serviceKeys = model.FindEntityType(typeof(Service))!.GetForeignKeys().ToList();
        var toRoute = Assert.Single(serviceKeys);
        Assert.Equal("FK_Services_Routes_RouteId", toRoute.GetConstraintName());
        Assert.Equal(typeof(Route), toRoute.PrincipalEntityType.ClrType);
        Assert.Equal("network", toRoute.PrincipalEntityType.GetSchema());
        Assert.Equal([nameof(Service.RouteId)], toRoute.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.NoAction, toRoute.DeleteBehavior);
        Assert.Null(toRoute.DependentToPrincipal);
        Assert.Null(toRoute.PrincipalToDependent);

        var stopKeys = model.FindEntityType(typeof(ServiceStop))!.GetForeignKeys()
            .OrderBy(fk => fk.GetConstraintName(), StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            ["FK_ServiceStops_Services_ServiceId", "FK_ServiceStops_Stations_StationId"],
            stopKeys.Select(fk => fk.GetConstraintName()));
        Assert.All(stopKeys, fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));

        Assert.Equal(typeof(Service), stopKeys[0].PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Service.Stops), stopKeys[0].PrincipalToDependent?.Name);
        Assert.Null(stopKeys[0].DependentToPrincipal);

        Assert.Equal(typeof(Station), stopKeys[1].PrincipalEntityType.ClrType);
        Assert.Equal("network", stopKeys[1].PrincipalEntityType.GetSchema());
        Assert.Equal([nameof(ServiceStop.StationId)], stopKeys[1].Properties.Select(p => p.Name));
        Assert.Null(stopKeys[1].DependentToPrincipal);
        Assert.Null(stopKeys[1].PrincipalToDependent);

        // No Network entity gained a navigation to a Timetable type.
        Assert.DoesNotContain(
            model.FindEntityType(typeof(Route))!.GetNavigations().Concat(model.FindEntityType(typeof(Station))!.GetNavigations()),
            navigation => navigation.TargetEntityType.ClrType.Namespace == typeof(Service).Namespace);

        // The migration agrees: three keys, all NO ACTION, the two cross-schema ones to network.
        var tables = new ExposedTimetableCreateServices().GetUpOperations().OfType<CreateTableOperation>().ToList();
        Assert.Equal(
            [
                "FK_ServiceStops_Services_ServiceId:timetable.Services:NoAction",
                "FK_ServiceStops_Stations_StationId:network.Stations:NoAction",
                "FK_Services_Routes_RouteId:network.Routes:NoAction",
            ],
            tables.SelectMany(table => table.ForeignKeys)
                .Select(fk => $"{fk.Name}:{fk.PrincipalSchema}.{fk.PrincipalTable}:{fk.OnDelete}")
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TimetableModel_DeclaresEveryCheckConstraint()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        Assert.Equal(
            [
                "CK_Services_CreatedAtUtc_Utc=DATEPART(TZOFFSET, [CreatedAtUtc]) = 0",
                "CK_Services_Direction=[Direction] IN (N'Forward', N'Reverse')",
                "CK_Services_EffectivePeriod=[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL",
                "CK_Services_OperatingDays=[RunsOnMonday] = 1 OR [RunsOnTuesday] = 1 OR [RunsOnWednesday] = 1 OR [RunsOnThursday] = 1 OR [RunsOnFriday] = 1 OR [RunsOnSaturday] = 1 OR [RunsOnSunday] = 1",
                "CK_Services_WithdrawnAtUtc_Utc=[WithdrawnAtUtc] IS NULL OR DATEPART(TZOFFSET, [WithdrawnAtUtc]) = 0",
            ],
            model.FindEntityType(typeof(Service))!.GetCheckConstraints()
                .Select(check => $"{check.Name}={check.Sql}")
                .Order(StringComparer.Ordinal));

        Assert.Equal(
            ["CK_ServiceStops_Position=[Position] >= 1"],
            model.FindEntityType(typeof(ServiceStop))!.GetCheckConstraints().Select(check => $"{check.Name}={check.Sql}"));
    }

    /// <summary>
    /// ADR-0025 item 6, plan O3: the migration creates the timetable schema and its two tables, and
    /// emits nothing against <c>network</c>; the two keys only reference it.
    /// </summary>
    [Fact]
    public void UpMigration_TimetableCreateServices_TouchesOnlyTheTimetableSchema()
    {
        var operations = new ExposedTimetableCreateServices().GetUpOperations();

        Assert.Equal(
            [
                "EnsureSchemaOperation:timetable",
                "CreateTableOperation:timetable.Services",
                "CreateTableOperation:timetable.ServiceStops",
                "CreateIndexOperation:timetable.Services",
                "CreateIndexOperation:timetable.Services",
                "CreateIndexOperation:timetable.ServiceStops",
            ],
            operations.Select(Describe));
        Assert.All(operations, operation => Assert.DoesNotContain("network", Describe(operation), StringComparison.Ordinal));
        Assert.DoesNotContain(operations, operation => operation is SqlOperation);
    }

    /// <summary>V8, rollback: the down-migration drops both tables, children first, then the schema.</summary>
    [Fact]
    public void DownMigration_TimetableCreateServices_DropsTablesAndTimetableSchema()
    {
        var operations = new ExposedTimetableCreateServices().GetDownOperations();

        Assert.Equal(
            [
                "DropTableOperation:timetable.ServiceStops",
                "DropTableOperation:timetable.Services",
                "DropSchemaOperation:timetable",
            ],
            operations.Select(Describe));
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        EnsureSchemaOperation schema => $"{nameof(EnsureSchemaOperation)}:{schema.Name}",
        DropSchemaOperation schema => $"{nameof(DropSchemaOperation)}:{schema.Name}",
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
            .Select(index => $"{index.GetDatabaseName()}:{string.Join(",", index.Properties.Select(p => p.Name))}:{index.IsUnique}")
            .Order(StringComparer.Ordinal)];

    private static YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options);

    private static IModel DesignTimeModel(YcrDbContext context) => context.GetService<IDesignTimeModel>().Model;

    private sealed class ExposedTimetableCreateServices : Timetable_CreateServices
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
