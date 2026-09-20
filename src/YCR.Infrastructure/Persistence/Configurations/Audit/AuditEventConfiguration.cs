using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YCR.Infrastructure.Audit;

namespace YCR.Infrastructure.Persistence.Configurations.Audit;

/// <summary>
/// Maps <see cref="AuditEvent"/> onto the append-only ledger table so EF can INSERT into it,
/// while <see cref="TableBuilder.ExcludeFromMigrations"/> keeps EF out of its DDL entirely.
/// </summary>
/// <remarks>
/// The table is created by raw SQL in <c>Audit_CreateAuditEventsLedger</c> (ADR-0017 item 1).
/// Excluding it from migrations is what makes ADR-0017 item 6 mechanical rather than a promise:
/// a future model change cannot produce a migration that converts or drops the ledger table,
/// because EF never emits DDL for it at all.
/// <para>
/// The column types are stated explicitly rather than inferred. They have to match the raw-SQL
/// table exactly, and an inferred <c>nvarchar(max)</c> where the table says <c>nvarchar(100)</c>
/// would only surface as a runtime truncation on an append-only table that cannot be repaired.
/// </para>
/// </remarks>
internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents", "audit", table => table.ExcludeFromMigrations());

        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedNever();

        builder.Property(auditEvent => auditEvent.OccurredAtUtc)
            .HasColumnType("datetimeoffset(3)")
            .IsRequired();

        builder.Property(auditEvent => auditEvent.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.ActorUserId);

        builder.Property(auditEvent => auditEvent.ActorRole)
            .HasMaxLength(1000);

        builder.Property(auditEvent => auditEvent.SubjectType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.SubjectId);

        builder.Property(auditEvent => auditEvent.BeforeJson);

        builder.Property(auditEvent => auditEvent.AfterJson);

        builder.Property(auditEvent => auditEvent.CorrelationId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.ClientIp)
            .HasMaxLength(45);

        builder.Property(auditEvent => auditEvent.ReasonCode)
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.AuthorizedByPermission)
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.PayloadVersion)
            .IsRequired();
    }
}
