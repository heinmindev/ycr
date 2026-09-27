using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Timetable;

namespace YCR.Infrastructure.Persistence.Configurations.Timetable;

/// <summary>
/// <c>timetable.ScheduleVersionServices</c> (F-005 spec §7; plan §DB changes 1, P1).
/// </summary>
/// <remarks>
/// <c>PK_ScheduleVersionServices (ScheduleVersionId, ServiceId)</c> lists a service once per
/// version (R17) and covers the foreign key to the version. <c>IX_ScheduleVersionServices_ServiceId</c>
/// covers the foreign key to <c>timetable.Services</c> and answers "which versions list S" for the
/// withdrawal guard (R19); EF would add it by convention, so it is declared and named here (plan
/// O1, P21). The service is referenced by id only: same module, no navigation in either direction.
/// Rows are insert-only (R27, R32; the grants).
/// </remarks>
public sealed class ScheduleVersionServiceConfiguration : IEntityTypeConfiguration<ScheduleVersionService>
{
    public void Configure(EntityTypeBuilder<ScheduleVersionService> builder)
    {
        builder.ToTable("ScheduleVersionServices", "timetable");

        builder.HasKey(entry => new { entry.ScheduleVersionId, entry.ServiceId })
            .HasName("PK_ScheduleVersionServices");

        builder.HasIndex(entry => entry.ServiceId)
            .HasDatabaseName("IX_ScheduleVersionServices_ServiceId");

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(entry => entry.ServiceId)
            .HasConstraintName("FK_ScheduleVersionServices_Services_ServiceId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(entry => entry.StopTimes)
            .WithOne()
            .HasForeignKey(stopTime => new { stopTime.ScheduleVersionId, stopTime.ServiceId })
            .HasConstraintName("FK_ScheduleStopTimes_ScheduleVersionServices_ScheduleVersionId_ServiceId")
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(entry => entry.StopTimes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
