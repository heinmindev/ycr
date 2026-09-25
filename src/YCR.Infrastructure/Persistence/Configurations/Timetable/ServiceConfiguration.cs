using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Network;
using YCR.Domain.Timetable;

namespace YCR.Infrastructure.Persistence.Configurations.Timetable;

/// <summary>
/// <c>timetable.Services</c> (F-004 spec §7; plan §DB changes).
/// </summary>
/// <remarks>
/// No <c>rowversion</c> (E10). <c>EffectiveTo</c> is the only concurrency token, the backstop
/// behind the service-code lock (R36). Check constraints live in the model, not only in the
/// migration, so <c>has-pending-model-changes</c> sees drift (the F-001 R-4 ruling). Every index
/// is declared and named, including the one EF would add by convention for the route key (P16).
/// <para>
/// The route is referenced by a <c>NO ACTION</c> foreign key into <c>network.Routes</c> with no
/// navigation in either direction (R29; ADR-0025 items 5-7). This migration, the referencing
/// module's, creates it; the Network model does not change.
/// </para>
/// </remarks>
public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", "timetable", table =>
        {
            table.HasCheckConstraint(
                "CK_Services_Direction",
                "[Direction] IN (N'Forward', N'Reverse')");
            table.HasCheckConstraint(
                "CK_Services_OperatingDays",
                "[RunsOnMonday] = 1 OR [RunsOnTuesday] = 1 OR [RunsOnWednesday] = 1 OR [RunsOnThursday] = 1 OR [RunsOnFriday] = 1 OR [RunsOnSaturday] = 1 OR [RunsOnSunday] = 1");
            table.HasCheckConstraint(
                "CK_Services_EffectivePeriod",
                "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom] OR [WithdrawnAtUtc] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_Services_CreatedAtUtc_Utc",
                "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
            table.HasCheckConstraint(
                "CK_Services_WithdrawnAtUtc_Utc",
                "[WithdrawnAtUtc] IS NULL OR DATEPART(TZOFFSET, [WithdrawnAtUtc]) = 0");
        });
        builder.HasKey(service => service.Id).HasName("PK_Services");
        builder.Property(service => service.Id).ValueGeneratedNever();
        builder.Ignore(service => service.DomainEvents);
        builder.Ignore(service => service.NeverRuns);

        builder.Property(service => service.Code)
            .HasConversion(code => code.Value, value => ServiceCode.From(value))
            .HasMaxLength(10)
            .IsRequired();

        // R35: deliberately NOT unique. A code may be reused by a service whose period does not
        // overlap; the overlap rule is enforced under the code lock. This index serves that read
        // and the list order.
        builder.HasIndex(service => new { service.Code, service.EffectiveFrom })
            .HasDatabaseName("IX_Services_Code_EffectiveFrom");

        builder.HasIndex(service => service.RouteId)
            .HasDatabaseName("IX_Services_RouteId");

        builder.OwnsOne(service => service.Name, name =>
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

        builder.Property(service => service.Direction)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.OwnsOne(service => service.OperatingDays, days =>
        {
            days.Property(value => value.RunsOnMonday).HasColumnName("RunsOnMonday").IsRequired();
            days.Property(value => value.RunsOnTuesday).HasColumnName("RunsOnTuesday").IsRequired();
            days.Property(value => value.RunsOnWednesday).HasColumnName("RunsOnWednesday").IsRequired();
            days.Property(value => value.RunsOnThursday).HasColumnName("RunsOnThursday").IsRequired();
            days.Property(value => value.RunsOnFriday).HasColumnName("RunsOnFriday").IsRequired();
            days.Property(value => value.RunsOnSaturday).HasColumnName("RunsOnSaturday").IsRequired();
            days.Property(value => value.RunsOnSunday).HasColumnName("RunsOnSunday").IsRequired();
            days.Ignore(value => value.Days);
        });

        builder.Property(service => service.EffectiveFrom)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(service => service.EffectiveTo)
            .HasColumnType("date")
            .IsConcurrencyToken();

        builder.Property(service => service.CreatedAtUtc)
            .HasColumnType("datetimeoffset(3)")
            .IsRequired();

        builder.Property(service => service.WithdrawnAtUtc)
            .HasColumnType("datetimeoffset(3)");

        // The aggregate owns its stop rows. NO ACTION, not EF's default Cascade: nothing deletes a
        // service, and no grant would allow it (R33).
        builder.HasMany(service => service.Stops)
            .WithOne()
            .HasForeignKey(stop => stop.ServiceId)
            .HasConstraintName("FK_ServiceStops_Services_ServiceId")
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(service => service.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);

        // R29, ADR-0025: the foreign key exists; a navigation does not, in either direction.
        builder.HasOne<Route>()
            .WithMany()
            .HasForeignKey(service => service.RouteId)
            .HasConstraintName("FK_Services_Routes_RouteId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
