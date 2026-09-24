using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Domain.Identity;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary>
/// <c>identity.AuthSessions</c>: one row per sign-in, and that row is the refresh family (D12).
/// </summary>
public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        var reasons = string.Join(", ", Enum.GetNames<RevocationReason>().Select(name => $"N'{name}'"));

        builder.ToTable("AuthSessions", "identity", table =>
        {
            table.HasCheckConstraint(
                "CK_AuthSessions_Revocation_Consistent",
                "([RevokedAtUtc] IS NULL AND [RevocationReason] IS NULL) OR ([RevokedAtUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL)");
            table.HasCheckConstraint("CK_AuthSessions_RevocationReason", $"[RevocationReason] IN ({reasons})");
            table.HasCheckConstraint("CK_AuthSessions_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            IdentityConfiguration.UtcCheck(table, "AuthSessions", nameof(AuthSession.CreatedAtUtc));
            IdentityConfiguration.UtcCheck(table, "AuthSessions", nameof(AuthSession.ExpiresAtUtc));
            IdentityConfiguration.UtcCheck(table, "AuthSessions", nameof(AuthSession.RevokedAtUtc));
        });

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Ignore(session => session.DomainEvents);

        builder.HasOne<StaffUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(session => session.UserId).HasDatabaseName("IX_AuthSessions_UserId");

        IdentityConfiguration.Utc(builder.Property(session => session.CreatedAtUtc)).IsRequired();
        IdentityConfiguration.Utc(builder.Property(session => session.ExpiresAtUtc)).IsRequired();

        // Plan P15: two revocations racing (logout vs family reuse) produce one revocation and
        // one audit row — the loser's UPDATE ... WHERE RevokedAtUtc IS NULL affects no row.
        IdentityConfiguration.Utc(builder.Property(session => session.RevokedAtUtc)).IsConcurrencyToken();
        builder.Property(session => session.RevocationReason)
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.HasMany(session => session.Tokens)
            .WithOne()
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.Navigation(session => session.Tokens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
