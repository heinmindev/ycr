using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Network;

namespace YCR.Infrastructure.Persistence.Configurations.Network;

/// <summary>
/// <c>network.Routes</c> (F-003 spec §7; plan P1).
/// </summary>
/// <remarks>
/// No <c>rowversion</c> and no direction column (E1, OQ36). <c>IsActive</c> is the only
/// concurrency token, for deactivation (R17, E8). Check constraints live in the model, not only
/// in the migration, so <c>has-pending-model-changes</c> sees drift (the F-001 R-4 ruling).
/// </remarks>
public sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("Routes", "network", table =>
        {
            table.HasCheckConstraint(
                "CK_Routes_CreatedAtUtc_Utc",
                "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
            table.HasCheckConstraint(
                "CK_Routes_DeactivatedAtUtc_Utc",
                "[DeactivatedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DeactivatedAtUtc]) = 0");
        });
        builder.HasKey(route => route.Id).HasName("PK_Routes");
        builder.Property(route => route.Id).ValueGeneratedNever();
        builder.Ignore(route => route.DomainEvents);

        builder.Property(route => route.Code)
            .HasConversion(code => code.Value, value => RouteCode.From(value))
            .HasMaxLength(10)
            .IsRequired();

        // R14: unique across all routes, including inactive ones, because rows are never deleted.
        builder.HasIndex(route => route.Code)
            .IsUnique()
            .HasDatabaseName("UX_Routes_Code");

        builder.OwnsOne(route => route.Name, name =>
        {
            name.Property(value => value.En)
                .HasColumnName("NameEn")
                .HasMaxLength(100)
                .IsRequired();
            name.Property(value => value.My)
                .HasColumnName("NameMy")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.Property(route => route.IsClosed).IsRequired();

        builder.Property(route => route.IsActive)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(route => route.CreatedAtUtc)
            .HasColumnType("datetimeoffset(3)")
            .IsRequired();

        builder.Property(route => route.DeactivatedAtUtc)
            .HasColumnType("datetimeoffset(3)");

        // The aggregate owns its sequence rows. NO ACTION, not EF's default Cascade (plan O4):
        // nothing deletes a route, and no grant would allow it.
        builder.HasMany(route => route.Stations)
            .WithOne()
            .HasForeignKey(station => station.RouteId)
            .HasConstraintName("FK_RouteStations_Routes_RouteId")
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(route => route.Stations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
