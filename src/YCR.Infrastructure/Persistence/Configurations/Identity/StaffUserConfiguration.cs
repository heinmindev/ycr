using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// <c>identity.Users</c> (spec §7; plan F-002 §DB changes). Check constraints are declared in the
/// model, not only in the migration, so <c>has-pending-model-changes</c> sees drift (R-4).
/// </summary>
public sealed class StaffUserConfiguration : IEntityTypeConfiguration<StaffUser>
{
    /// <summary>Shadow <c>rowversion</c>: concurrent administrator edits vs sign-in counters (spec §7).</summary>
    public const string RowVersion = "RowVersion";

    public void Configure(EntityTypeBuilder<StaffUser> builder)
    {
        builder.ToTable("Users", "identity", table =>
        {
            // Binary collation, so upper case is refused whatever the database collation (R20).
            table.HasCheckConstraint(
                "CK_Users_UserName_Format",
                "LEN([UserName]) BETWEEN 3 AND 50 AND [UserName] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^a-z0-9.]%'");
            table.HasCheckConstraint(
                "CK_Users_Disabled_Consistent",
                "([IsDisabled] = 1 AND [DisabledAtUtc] IS NOT NULL) OR ([IsDisabled] = 0 AND [DisabledAtUtc] IS NULL)");
            table.HasCheckConstraint(
                "CK_Users_AccessFailedCount",
                $"[AccessFailedCount] BETWEEN 0 AND {AccountLockoutPolicy.MaxFailedAttempts}");
            IdentityConfiguration.UtcCheck(table, "Users", nameof(StaffUser.DisabledAtUtc));
            IdentityConfiguration.UtcCheck(table, "Users", nameof(StaffUser.LockoutEndUtc));
            IdentityConfiguration.UtcCheck(table, "Users", nameof(StaffUser.PasswordChangedAtUtc));
            IdentityConfiguration.UtcCheck(table, "Users", nameof(StaffUser.CreatedAtUtc));
        });

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Ignore(user => user.DomainEvents);

        builder.Property(user => user.UserName)
            .HasConversion(name => name.Value, value => UserName.From(value))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.NormalizedUserName).HasMaxLength(50).IsRequired();
        builder.HasIndex(user => user.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName(IdentityConstraints.UserNameUniqueIndex);

        // Identity's V3 hash is 84 characters; the column leaves room for a future format.
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(user => user.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(user => user.IsDisabled).IsRequired();
        IdentityConfiguration.Utc(builder.Property(user => user.DisabledAtUtc));
        IdentityConfiguration.Utc(builder.Property(user => user.LockoutEndUtc));
        builder.Property(user => user.AccessFailedCount).IsRequired();
        IdentityConfiguration.Utc(builder.Property(user => user.PasswordChangedAtUtc)).IsRequired();
        builder.Property(user => user.MustChangePassword).IsRequired();
        IdentityConfiguration.Utc(builder.Property(user => user.CreatedAtUtc)).IsRequired();

        builder.Property<byte[]>(RowVersion).IsRowVersion().IsRequired();

        // Roles are replaced by deleting and inserting rows (plan P7). The database FK is
        // NO ACTION like every FK in identity; ClientCascade lets EF delete the orphaned rows it
        // tracks when ReplaceRoles drops one, without an ON DELETE CASCADE in the schema.
        builder.HasMany(user => user.Roles)
            .WithOne()
            .HasForeignKey(role => role.UserId)
            .OnDelete(DeleteBehavior.ClientCascade);
        builder.Navigation(user => user.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
