using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContextFactory : IDesignTimeDbContextFactory<YcrDbContext>
{
    /// <summary>
    /// The environment variable that names the design-time target. It has no default: EF tooling
    /// must be told where to point, so an unconfigured <c>dotnet ef database update</c> cannot
    /// reach a real server (T-008b's C-8).
    /// </summary>
    /// <remarks>
    /// Public so the test fixture can pass the same name to the <c>dotnet ef</c> child process it
    /// spawns, rather than repeating the literal and letting the two drift (R-1).
    /// </remarks>
    public const string DesignTimeConnectionVariable = "YCR_DESIGN_TIME_CONNECTION";

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
