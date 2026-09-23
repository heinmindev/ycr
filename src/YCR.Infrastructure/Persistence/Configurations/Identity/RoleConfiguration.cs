using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// <c>identity.Roles</c>: the catalogue, seeded by migration and read-only to the application
/// (D8; <c>ycr_app</c> has <c>SELECT</c> only).
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "identity");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).ValueGeneratedNever();
        builder.Property(role => role.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique().HasDatabaseName("UX_Roles_Name");

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(role => role.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
