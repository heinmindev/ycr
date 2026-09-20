using Microsoft.EntityFrameworkCore;
using YCR.Domain.Network;
using YCR.Infrastructure.Identifiers;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Persistence;

public sealed class StationModelTests
{
    [Fact]
    public void Model_MapsStationTableAndCodeIndex()
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
        Assert.Equal("UX_Stations_Code", codeIndex.GetDatabaseName());

        var isActive = entity.FindProperty(nameof(Station.IsActive));
        Assert.NotNull(isActive);
        Assert.True(isActive.IsConcurrencyToken);
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
}
