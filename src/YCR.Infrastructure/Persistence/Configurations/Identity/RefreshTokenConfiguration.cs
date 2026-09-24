using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Application.Identity;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// <c>identity.RefreshTokens</c>: hashes only (R5, S31); at most one successor per token,
/// enforced by the database (R6, S33; plan P15).
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", "identity", table =>
        {
            table.HasCheckConstraint(
                "CK_RefreshTokens_Rotation_Consistent",
                "([RotatedAtUtc] IS NULL AND [ReplacedByTokenId] IS NULL) OR ([RotatedAtUtc] IS NOT NULL AND [ReplacedByTokenId] IS NOT NULL)");
            table.HasCheckConstraint("CK_RefreshTokens_NotSelf", "[ReplacedByTokenId] <> [Id]");
            IdentityConfiguration.UtcCheck(table, "RefreshTokens", nameof(RefreshToken.IssuedAtUtc));
            IdentityConfiguration.UtcCheck(table, "RefreshTokens", nameof(RefreshToken.RotatedAtUtc));
        });

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Ignore(token => token.IsRotated);

        builder.Property(token => token.TokenHash)
            .HasColumnType($"binary({RefreshToken.HashLength})")
            .IsRequired();
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName(IdentityConstraints.RefreshTokenHashUniqueIndex);

        IdentityConfiguration.Utc(builder.Property(token => token.IssuedAtUtc)).IsRequired();
        IdentityConfiguration.Utc(builder.Property(token => token.RotatedAtUtc));

        // Plan P15 / V4: the rotation UPDATE carries WHERE [ReplacedByTokenId] IS NULL, so a
        // token moves from current to rotated exactly once; a losing rotation affects no row.
        builder.Property(token => token.ReplacedByTokenId).IsConcurrencyToken();
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(token => token.ReplacedByTokenId)
            .IsUnique()
            .HasFilter("[ReplacedByTokenId] IS NOT NULL")
            .HasDatabaseName(IdentityConstraints.RefreshTokenSuccessorUniqueIndex);

        builder.HasIndex(token => token.SessionId).HasDatabaseName("IX_RefreshTokens_SessionId");
    }
}
