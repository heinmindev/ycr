using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Timetable;

namespace YCR.Infrastructure.Persistence.Configurations.Timetable;

/// <summary>
/// <c>timetable.ScheduleVersions</c> (F-005 spec §7; plan §DB changes 1).
/// </summary>
/// <remarks>
/// No <c>rowversion</c> and no client-held version (R47, E7): <c>Status</c> is the only
/// concurrency token, the backstop behind the Timetable-wide lock, so each transition is one
/// guarded <c>UPDATE</c> of the status and one instant (plan V1, V9). Check constraints live in the
/// model, not only in the migration, so <c>has-pending-model-changes</c> sees drift (the F-001 R-4
/// ruling). Every index is declared and named (plan P21).
/// <para>
/// <c>UX_ScheduleVersions_EffectiveFrom_Published</c> is R22's authority: start dates are unique
/// among published versions only (a filtered unique index), and a violation maps to <c>409</c> by
/// its name (<c>TimetableConstraints</c>). It is also the in-force and coverage scan (plan O3).
/// </para>
/// </remarks>
public sealed class ScheduleVersionConfiguration : IEntityTypeConfiguration<ScheduleVersion>
{
    public void Configure(EntityTypeBuilder<ScheduleVersion> builder)
    {
        builder.ToTable("ScheduleVersions", "timetable", table =>
        {
            table.HasCheckConstraint(
                "CK_ScheduleVersions_Number",
                "[Number] >= 1");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_Status",
                "[Status] IN (N'Draft', N'Published', N'Discarded', N'Cancelled')");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_StatusInstants",
                "([Status] = N'Draft' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Published' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Discarded' AND [PublishedAtUtc] IS NULL AND [DiscardedAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = N'Cancelled' AND [PublishedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_CreatedAtUtc_Utc",
                "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_PublishedAtUtc_Utc",
                "[PublishedAtUtc] IS NULL OR DATEPART(TZOFFSET, [PublishedAtUtc]) = 0");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_DiscardedAtUtc_Utc",
                "[DiscardedAtUtc] IS NULL OR DATEPART(TZOFFSET, [DiscardedAtUtc]) = 0");
            table.HasCheckConstraint(
                "CK_ScheduleVersions_CancelledAtUtc_Utc",
                "[CancelledAtUtc] IS NULL OR DATEPART(TZOFFSET, [CancelledAtUtc]) = 0");
        });
        builder.HasKey(version => version.Id).HasName("PK_ScheduleVersions");
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Ignore(version => version.DomainEvents);

        builder.Property(version => version.Number)
            .ValueGeneratedNever()
            .IsRequired();

        // R7 backstop behind the lock: numbers are unique and never reused.
        builder.HasIndex(version => version.Number)
            .IsUnique()
            .HasDatabaseName("UX_ScheduleVersions_Number");

        builder.OwnsOne(version => version.Name, name =>
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

        builder.Property(version => version.EffectiveFrom)
            .HasColumnType("date")
            .IsRequired();

        // R22: the one authority for "no two published versions share a start date". Mapped to
        // 409 Timetable.ScheduleVersionEffectiveFromTaken by name (TimetableConstraints).
        builder.HasIndex(version => version.EffectiveFrom)
            .IsUnique()
            .HasFilter("[Status] = N'Published'")
            .HasDatabaseName("UX_ScheduleVersions_EffectiveFrom_Published");

        builder.Property(version => version.Status)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(version => version.CreatedAtUtc)
            .HasColumnType("datetimeoffset(3)")
            .IsRequired();

        builder.Property(version => version.PublishedAtUtc)
            .HasColumnType("datetimeoffset(3)");

        builder.Property(version => version.DiscardedAtUtc)
            .HasColumnType("datetimeoffset(3)");

        builder.Property(version => version.CancelledAtUtc)
            .HasColumnType("datetimeoffset(3)");

        // The aggregate owns its entries. NO ACTION, not EF's default Cascade: nothing deletes a
        // version, and no grant would allow it (R25).
        builder.HasMany(version => version.Services)
            .WithOne()
            .HasForeignKey(entry => entry.ScheduleVersionId)
            .HasConstraintName("FK_ScheduleVersionServices_ScheduleVersions_ScheduleVersionId")
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(version => version.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
