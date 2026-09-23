using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Identity;
using YCR.Application.Network;
using YCR.Domain.Identity;
using YCR.Domain.Network;
using YCR.Infrastructure.Audit;

namespace YCR.Infrastructure.Persistence;

public sealed class YcrDbContext(DbContextOptions<YcrDbContext> options)
    : DbContext(options), INetworkDbContext, IIdentityDbContext
{
    public DbSet<Station> Stations => Set<Station>();

    public DbSet<StaffUser> Users => Set<StaffUser>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

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

    /// <summary>
    /// Saves, translating SQL Server unique-constraint errors into the provider-neutral
    /// <see cref="UniqueConstraintViolationException"/> (plan §Unique-constraint translation).
    /// </summary>
    /// <remarks>
    /// The translation lives here rather than in an <c>ISaveChangesInterceptor</c> because EF's
    /// failure interceptor observes an exception but cannot replace it, and replacing it is the
    /// whole point: Application must never see <c>SqlException</c>.
    /// </remarks>
    /// <remarks>
    /// This overload, not the one-argument one, because every other <c>SaveChangesAsync</c>
    /// overload delegates here. Overriding the shorter one would leave a direct
    /// <c>SaveChangesAsync(acceptAllChangesOnSuccess, ct)</c> call untranslated.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception)
            when (SqlServerUniqueConstraintTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    /// <inheritdoc cref="SaveChangesAsync(bool, CancellationToken)" />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception)
            when (SqlServerUniqueConstraintTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }
}
