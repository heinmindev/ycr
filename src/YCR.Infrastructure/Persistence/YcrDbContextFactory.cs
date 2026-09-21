using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContextFactory : IDesignTimeDbContextFactory<YcrDbContext>
{
    private const string DesignTimeConnectionVariable = "YCR_DESIGN_TIME_CONNECTION";

    public YcrDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DesignTimeConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{DesignTimeConnectionVariable} must be set for EF design-time operations.");
        }

        var options = new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new YcrDbContext(options);
    }
}
