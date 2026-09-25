using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Network;

namespace YCR.Infrastructure.Persistence.Configurations.Network;

/// <summary>
/// <c>network.RouteStations</c> (F-003 spec §7; plan P1, P7).
/// </summary>
/// <remarks>
/// <c>UX_RouteStations_StationId_RouteId</c> leads with <c>StationId</c> on purpose (spec
/// Amendment 1, hein, 2026-09-24). It enforces R7, and because it leads with the foreign-key
/// column it also covers <c>FK_RouteStations_Stations_StationId</c>, so EF's foreign-key index
/// convention adds no <c>IX_RouteStations_StationId</c>. No F-003 query filters by station alone.
/// Reversing the columns brings that index back; <c>RouteModelTests</c> would fail.
/// </remarks>
public sealed class RouteStationConfiguration : IEntityTypeConfiguration<RouteStation>
{
    public void Configure(EntityTypeBuilder<RouteStation> builder)
    {
        builder.ToTable("RouteStations", "network", table =>
            table.HasCheckConstraint("CK_RouteStations_Position", "[Position] >= 1"));

        builder.HasKey(station => new { station.RouteId, station.Position }).HasName("PK_RouteStations");
        builder.Property(station => station.Position).ValueGeneratedNever();

        builder.HasIndex(station => new { station.StationId, station.RouteId })
            .IsUnique()
            .HasDatabaseName("UX_RouteStations_StationId_RouteId");

        // E5: the foreign key exists; a navigation does not, in either direction.
        builder.HasOne<Station>()
            .WithMany()
            .HasForeignKey(station => station.StationId)
            .HasConstraintName("FK_RouteStations_Stations_StationId")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
