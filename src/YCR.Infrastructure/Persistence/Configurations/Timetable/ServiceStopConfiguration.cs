using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Network;
using YCR.Domain.Timetable;

namespace YCR.Infrastructure.Persistence.Configurations.Timetable;

/// <summary>
/// <c>timetable.ServiceStops</c> (F-004 spec §7; plan P1, P16).
/// </summary>
/// <remarks>
/// <c>PK_ServiceStops (ServiceId, Position)</c> also covers the service foreign key, so EF adds no
/// <c>IX_ServiceStops_ServiceId</c> (plan O2). <c>IX_ServiceStops_StationId</c> covers the station
/// foreign key; it is declared and named here although EF would add it by convention, so that it is
/// deliberate. It is not unique: the full-circuit closure repeats a station (R14). Contiguity,
/// order, the closure and the minimum are the aggregate's, because a check constraint cannot see
/// other rows. Rows are insert-only (R20; the grants).
/// </remarks>
public sealed class ServiceStopConfiguration : IEntityTypeConfiguration<ServiceStop>
{
    public void Configure(EntityTypeBuilder<ServiceStop> builder)
    {
        builder.ToTable("ServiceStops", "timetable", table =>
            table.HasCheckConstraint("CK_ServiceStops_Position", "[Position] >= 1"));

        builder.HasKey(stop => new { stop.ServiceId, stop.Position }).HasName("PK_ServiceStops");
        builder.Property(stop => stop.Position).ValueGeneratedNever();

        builder.HasIndex(stop => stop.StationId)
            .HasDatabaseName("IX_ServiceStops_StationId");

        // R29, ADR-0025: the foreign key exists; a navigation does not, in either direction.
        builder.HasOne<Station>()
            .WithMany()
            .HasForeignKey(stop => stop.StationId)
            .HasConstraintName("FK_ServiceStops_Stations_StationId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
