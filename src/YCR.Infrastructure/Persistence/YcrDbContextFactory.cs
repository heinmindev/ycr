using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContextFactory : IDesignTimeDbContextFactory<YcrDbContext>
{
    public YcrDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<YcrDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=YcrDesignTime")
            .Options;

        return new YcrDbContext(options);
    }
}
