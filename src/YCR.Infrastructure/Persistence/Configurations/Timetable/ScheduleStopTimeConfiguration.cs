using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YCR.Domain.Timetable;

namespace YCR.Infrastructure.Persistence.Configurations.Timetable;

/// <summary>
/// <c>timetable.ScheduleStopTimes</c> (F-005 spec §7; plan §DB changes 1, P1, P21).
/// </summary>
/// <remarks>
/// <para>
/// A stop time names the service's own stop by <c>(ServiceId, Position)</c>, a <c>NO ACTION</c>
/// foreign key to <c>PK_ServiceStops</c> (spec D16), so the database is the authority that a time
/// names a real stop of a real service. EF adds an index for that key by convention because the
/// primary key leads with <c>ScheduleVersionId</c>; it is declared and named here as
/// <c>IX_ScheduleStopTimes_ServiceId_Position</c>, not unique (one row per version) (plan O1, P21).
/// </para>
/// <para>
/// Times are ADR-0027 minutes, <c>smallint</c> <c>0..1439</c>; a missing time is <c>NULL</c> (a
/// converter is never applied to a null). The checks refuse a minute outside the day, a row with no
/// time and a negative dwell from any writer (R10, R12, R15, R16). The first- and last-stop rules,
/// contiguity and ordering across stops are the aggregate's: a check cannot see other rows. Rows
/// are insert-only (R27, R32; the grants).
/// </para>
/// </remarks>
public sealed class ScheduleStopTimeConfiguration : IEntityTypeConfiguration<ScheduleStopTime>
{
    // EF never passes a null to a converter (plan V7), so the null-forgiving operator is safe.
    private static readonly ValueConverter<TimetableTime?, short> Minutes = new(
        time => time!.Minutes,
        minutes => TimetableTime.FromMinutes(minutes));

    public void Configure(EntityTypeBuilder<ScheduleStopTime> builder)
    {
        builder.ToTable("ScheduleStopTimes", "timetable", table =>
        {
            table.HasCheckConstraint(
                "CK_ScheduleStopTimes_Minutes",
                "([ArrivalMinute] IS NULL OR [ArrivalMinute] BETWEEN 0 AND 1439) AND ([DepartureMinute] IS NULL OR [DepartureMinute] BETWEEN 0 AND 1439)");
            table.HasCheckConstraint(
                "CK_ScheduleStopTimes_AnyTime",
                "[ArrivalMinute] IS NOT NULL OR [DepartureMinute] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_ScheduleStopTimes_Dwell",
                "[ArrivalMinute] IS NULL OR [DepartureMinute] IS NULL OR [DepartureMinute] >= [ArrivalMinute]");
        });

        builder.HasKey(stopTime => new { stopTime.ScheduleVersionId, stopTime.ServiceId, stopTime.Position })
            .HasName("PK_ScheduleStopTimes");
        builder.Property(stopTime => stopTime.Position).ValueGeneratedNever();

        builder.Property(stopTime => stopTime.Arrival)
            .HasColumnName("ArrivalMinute")
            .HasColumnType("smallint")
            .HasConversion(Minutes);

        builder.Property(stopTime => stopTime.Departure)
            .HasColumnName("DepartureMinute")
            .HasColumnType("smallint")
            .HasConversion(Minutes);

        builder.HasIndex(stopTime => new { stopTime.ServiceId, stopTime.Position })
            .HasDatabaseName("IX_ScheduleStopTimes_ServiceId_Position");

        // Spec D16, E2: the stop exists. Same module, no navigation in either direction.
        builder.HasOne<ServiceStop>()
            .WithMany()
            .HasForeignKey(stopTime => new { stopTime.ServiceId, stopTime.Position })
            .HasConstraintName("FK_ScheduleStopTimes_ServiceStops_ServiceId_Position")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
