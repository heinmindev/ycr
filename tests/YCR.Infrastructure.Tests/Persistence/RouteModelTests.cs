using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using YCR.Application.Network;
using YCR.Domain.Common;
using YCR.Domain.Network;
using YCR.Infrastructure.Persistence;
using YCR.Infrastructure.Persistence.Migrations;

namespace YCR.Infrastructure.Tests.Persistence;

/// <summary>
/// F-003 spec §7 and plan §DB changes: the route tables as the EF model declares them.
/// </summary>
/// <remarks>
/// Asserted against the design-time model where it matters (check constraints, indexes), because
/// that is the model <c>has-pending-model-changes</c> diffs; the runtime model drops check
/// constraints.
/// </remarks>
public sealed class RouteModelTests
{
    [Fact]
    public void RouteModel_MapsTablesKeysAndConstraintNames()
    {
        using var context = NewContext();
        var model = DesignTimeModel(context);

        var routes = model.FindEntityType(typeof(Route))!;
        Assert.Equal("Routes", routes.GetTableName());
        Assert.Equal("network", routes.GetSchema());
        Assert.Equal("PK_Routes", routes.FindPrimaryKey()!.GetName());
        Assert.Equal([nameof(Route.Id)], routes.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(ValueGenerated.Never, routes.FindProperty(nameof(Route.Id))!.ValueGenerated);

        // CreateRouteHandler matches on these constants to turn a duplicate into a 409 or a 422;
        // a rename here without one there would turn it into an unhandled 500.
        var code = Assert.Single(routes.GetIndexes());
        Assert.True(code.IsUnique);
        Assert.Equal([nameof(Route.Code)], code.Properties.Select(p => p.Name));
        Assert.Equal(NetworkConstraints.RouteCodeUniqueIndex, code.GetDatabaseName());
        Assert.Equal("UX_Routes_Code", NetworkConstraints.RouteCodeUniqueIndex);

        // R17 / E8: IsActive is the only concurrency token, and there is no rowversion.
        Assert.Equal(
            [nameof(Route.IsActive)],
            routes.GetProperties().Where(p => p.IsConcurrencyToken).Select(p => p.Name));
        Assert.DoesNotContain(routes.GetProperties(), p => p.GetColumnType() == "rowversion");
        Assert.DoesNotContain(routes.GetProperties(), p => p.Name == nameof(AggregateRoot.DomainEvents));

        Assert.Equal("nvarchar(10)", routes.FindProperty(nameof(Route.Code))!.GetColumnType());
        Assert.Equal("datetimeoffset(3)", routes.FindProperty(nameof(Route.CreatedAtUtc))!.GetColumnType());
        Assert.Equal("datetimeoffset(3)", routes.FindProperty(nameof(Route.DeactivatedAtUtc))!.GetColumnType());
        Assert.True(routes.FindProperty(nameof(Route.DeactivatedAtUtc))!.IsNullable);
        var name = routes.FindNavigation(nameof(Route.Name))!.TargetEntityType;
        Assert.Equal("NameEn", name.FindProperty(nameof(BilingualName.En))!.GetColumnName());
        Assert.Equal("NameMy", name.FindProperty(nameof(BilingualName.My))!.GetColumnName());
        Assert.Equal("nvarchar(100)", name.FindProperty(nameof(BilingualName.En))!.GetColumnType());
        Assert.Equal("nvarchar(100)", name.FindProperty(nameof(BilingualName.My))!.GetColumnType());

        var stations = model.FindEntityType(typeof(RouteStation))!;
        Assert.Equal("RouteStations", stations.GetTableName());
        Assert.Equal("network", stations.GetSchema());
        Assert.Equal("PK_RouteStations", stations.FindPrimaryKey()!.GetName());
        Assert.Equal(
            [nameof(RouteStation.RouteId), nameof(RouteStation.Position)],
            stations.FindPrimaryKey()!.Properties.Select(p => p.Name));

        var unique = Assert.Single(stations.GetIndexes(), index => index.IsUnique);
        Assert.Equal(
            [nameof(RouteStation.StationId), nameof(RouteStation.RouteId)],
            unique.Properties.Select(p => p.Name));
        Assert.Equal(NetworkConstraints.RouteStationUniqueIndex, unique.GetDatabaseName());
        Assert.Equal("UX_RouteStations_StationId_RouteId", NetworkConstraints.RouteStationUniqueIndex);
    }

    /// <summary>
    /// P7 / spec Amendment 1: <c>RouteStations</c> has exactly its primary key and the unique
    /// index. EF adds <c>IX_RouteStations_StationId</c> for the station foreign key unless another
    /// index leads with <c>StationId</c> (plan O1-O3), so this is what keeps it dropped.
    /// </summary>
    [Fact]
    public void RouteModel_RouteStationsHasNoStationIdOnlyIndex()
    {
        using var context = NewContext();
        var stations = DesignTimeModel(context).FindEntityType(typeof(RouteStation))!;

        Assert.Equal(
            ["UX_RouteStations_StationId_RouteId"],
            stations.GetIndexes().Select(index => index.GetDatabaseName()));

        // And the migration that builds the table creates no such index either.
        var up = new ExposedNetworkCreateRoutes().GetUpOperations();
        var created = up.OfType<CreateIndexOperation>().Where(index => index.Table == "RouteStations").ToList();
        Assert.Equal(["UX_RouteStations_StationId_RouteId"], created.Select(index => index.Name));
        Assert.Equal(["StationId", "RouteId"], created[0].Columns);
        Assert.True(created[0].IsUnique);
        Assert.DoesNotContain(up.OfType<CreateIndexOperation>(), index => index.Name == "IX_RouteStations_StationId");
    }

    [Fact]
    public void RouteModel_ForeignKeys_AreNoActionWithoutStationNavigation()
    {
        using var context = NewContext();
        var stations = DesignTimeModel(context).FindEntityType(typeof(RouteStation))!;

        var foreignKeys = stations.GetForeignKeys().OrderBy(fk => fk.GetConstraintName(), StringComparer.Ordinal).ToList();
        Assert.Equal(
            ["FK_RouteStations_Routes_RouteId", "FK_RouteStations_Stations_StationId"],
            foreignKeys.Select(fk => fk.GetConstraintName()));
        Assert.All(foreignKeys, fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));

        Assert.Equal(typeof(Route), foreignKeys[0].PrincipalEntityType.ClrType);
        Assert.Equal([nameof(RouteStation.RouteId)], foreignKeys[0].Properties.Select(p => p.Name));
        Assert.Equal(typeof(Station), foreignKeys[1].PrincipalEntityType.ClrType);
        Assert.Equal([nameof(RouteStation.StationId)], foreignKeys[1].Properties.Select(p => p.Name));

        // E5: no navigation in either direction between a route row and a station.
        Assert.Null(foreignKeys[1].DependentToPrincipal);
        Assert.Null(foreignKeys[1].PrincipalToDependent);

        // The only navigation into RouteStation is the aggregate's own collection.
        Assert.Equal(nameof(Route.Stations), foreignKeys[0].PrincipalToDependent?.Name);
        Assert.Null(foreignKeys[0].DependentToPrincipal);

        // The migration agrees: both FKs NO ACTION (EF's default for the route FK is Cascade, O4).
        var up = new ExposedNetworkCreateRoutes().GetUpOperations();
        var table = Assert.Single(up.OfType<CreateTableOperation>(), operation => operation.Name == "RouteStations");
        Assert.Equal(2, table.ForeignKeys.Count);
        Assert.All(table.ForeignKeys, fk => Assert.Equal(ReferentialAction.NoAction, fk.OnDelete));
    }

    [Fact]
    public void RouteModel_DeclaresPositionCheck()
    {
        using var context = NewContext();
        var stations = DesignTimeModel(context).FindEntityType(typeof(RouteStation))!;

        var check = Assert.Single(stations.GetCheckConstraints());
        Assert.Equal("CK_RouteStations_Position", check.Name);
        Assert.Equal("[Position] >= 1", check.Sql);
    }

    /// <summary>
    /// Rollback (plan §DB changes): the down-migration drops the two route tables, children first,
    /// and never the <c>network</c> schema, which <c>Network_CreateStations</c> owns.
    /// </summary>
    [Fact]
    public void DownMigration_NetworkCreateRoutes_DropsTablesButNotNetworkSchema()
    {
        var operations = new ExposedNetworkCreateRoutes().GetDownOperations();

        Assert.DoesNotContain(operations, operation => operation is DropSchemaOperation);
        Assert.Equal(
            ["RouteStations", "Routes"],
            operations.OfType<DropTableOperation>().Select(operation => operation.Name));
        Assert.All(operations.OfType<DropTableOperation>(), operation => Assert.Equal("network", operation.Schema));
        Assert.DoesNotContain(operations, operation => operation is DropTableOperation { Name: "Stations" });
    }

    private static YcrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrModelTest")
            .Options);

    private static IModel DesignTimeModel(YcrDbContext context) => context.GetService<IDesignTimeModel>().Model;

    private sealed class ExposedNetworkCreateRoutes : Network_CreateRoutes
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
