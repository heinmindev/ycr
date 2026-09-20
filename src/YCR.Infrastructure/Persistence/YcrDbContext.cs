using Microsoft.EntityFrameworkCore;
using YCR.Application.Network;
using YCR.Domain.Network;
using YCR.Infrastructure.Audit;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContext(DbContextOptions<YcrDbContext> options)
    : DbContext(options), INetworkDbContext
{
    public DbSet<Station> Stations => Set<Station>();

    /// <summary>
    /// The audit ledger. Internal on purpose: no module context interface exposes it, so the
    /// only way into <c>audit.AuditEvents</c> from above Infrastructure is
    /// <see cref="Application.Common.Abstractions.IAuditWriter"/>, which derives the actor
    /// fields server-side (ADR-0017 item 2).
    /// </summary>
    internal DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YcrDbContext).Assembly);
    }
}
