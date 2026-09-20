using Microsoft.EntityFrameworkCore;
using YCR.Application.Network;
using YCR.Domain.Network;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContext(DbContextOptions<YcrDbContext> options)
    : DbContext(options), INetworkDbContext
{
    public DbSet<Station> Stations => Set<Station>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YcrDbContext).Assembly);
    }
}
