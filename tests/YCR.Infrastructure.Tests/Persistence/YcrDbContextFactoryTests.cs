using Microsoft.EntityFrameworkCore;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Persistence;

[Collection(DesignTimeConnectionCollection.Name)]
public sealed class YcrDbContextFactoryTests
{
    private const string VariableName = "YCR_DESIGN_TIME_CONNECTION";

    [Fact]
    public void CreateDbContext_WithoutDesignTimeConnection_ThrowsClearMessage()
    {
        var original = Environment.GetEnvironmentVariable(VariableName);
        try
        {
            Environment.SetEnvironmentVariable(VariableName, null);

            var failure = Assert.Throws<InvalidOperationException>(() =>
                new YcrDbContextFactory().CreateDbContext([]));

            Assert.Equal(
                $"{VariableName} must be set for EF design-time operations.",
                failure.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, original);
        }
    }

    [Fact]
    public void CreateDbContext_WithDesignTimeConnection_UsesConfiguredConnection()
    {
        var original = Environment.GetEnvironmentVariable(VariableName);
        const string connectionString =
            "Server=design-time-server;Database=YcrDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            Environment.SetEnvironmentVariable(VariableName, connectionString);

            using var context = new YcrDbContextFactory().CreateDbContext([]);

            Assert.Equal("design-time-server", context.Database.GetDbConnection().DataSource);
            Assert.Equal("YcrDesignTime", context.Database.GetDbConnection().Database);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, original);
        }
    }
}
