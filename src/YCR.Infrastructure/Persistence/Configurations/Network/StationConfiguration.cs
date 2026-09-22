using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Network;

namespace YCR.Infrastructure.Persistence.Configurations.Network;

public sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> builder)
    {
        // R-4 (hein's ruling, 2026-09-22): check constraints live in the EF model, not only in a
        // migration. A constraint EF does not know about cannot be diffed — drop it in the
        // database and `dotnet ef migrations has-pending-model-changes` stays silent. Declaring it
        // here puts it in the snapshot, so the CI step that runs that command is what notices.
        builder.ToTable(
            "Stations",
            "network",
            table => table.HasCheckConstraint(
                "CK_Stations_CreatedAtUtc_Utc",
                "DATEPART(TZOFFSET, [CreatedAtUtc]) = 0"));
        builder.HasKey(station => station.Id);
        builder.Property(station => station.Id).ValueGeneratedNever();
        builder.Ignore(station => station.DomainEvents);

        builder.Property(station => station.Code)
            .HasConversion(code => code.Value, value => StationCode.From(value))
            .HasMaxLength(10)
            .IsRequired();

        builder.HasIndex(station => station.Code)
            .IsUnique()
            .HasDatabaseName("UX_Stations_Code");

        builder.OwnsOne(station => station.Name, name =>
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

        builder.Property(station => station.IsActive)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(station => station.CreatedAtUtc)
            .HasColumnType("datetimeoffset(3)")
            .IsRequired();
    }
}
