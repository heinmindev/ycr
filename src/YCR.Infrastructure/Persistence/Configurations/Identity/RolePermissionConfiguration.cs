using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary><c>identity.RolePermissions</c>: role→permission grants as data (D8, R11).</summary>
public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", "identity", table => table.HasCheckConstraint(
            "CK_RolePermissions_Permission_Format",
            "[Permission] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^-a-z.]%'"));
        builder.HasKey(permission => new { permission.RoleId, permission.Permission });
        builder.Property(permission => permission.Permission).HasMaxLength(100).IsRequired();
    }
}
