using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary><c>identity.UserRoles</c>: a user may hold several roles (D7).</summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles", "identity");
        builder.HasKey(role => new { role.UserId, role.RoleId });

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(role => role.RoleId)
            .OnDelete(DeleteBehavior.NoAction);

        // The R27 count joins on RoleId (plan §DB changes).
        builder.HasIndex(role => role.RoleId).HasDatabaseName("IX_UserRoles_RoleId");
    }
}
