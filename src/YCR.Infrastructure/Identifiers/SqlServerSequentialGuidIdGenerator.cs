using Microsoft.EntityFrameworkCore.ValueGeneration;
using YCR.Application.Common.Abstractions;

namespace YCR.Infrastructure.Identifiers;

public sealed class SqlServerSequentialGuidIdGenerator : IIdGenerator
{
    private readonly SequentialGuidValueGenerator _generator = new();

    // Verified against EF Core 10.0.12: SequentialGuidValueGenerator ignores EntityEntry.
    // Re-check this assumption before upgrading EF Core; the wrapper intentionally keeps the
    // application-facing generator independent from EF's change-tracking types.
    public Guid New() => _generator.Next(null!);
}
