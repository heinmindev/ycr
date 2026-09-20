using Microsoft.EntityFrameworkCore.ValueGeneration;
using YCR.Application.Common.Abstractions;

namespace YCR.Infrastructure.Identifiers;

public sealed class SqlServerSequentialGuidIdGenerator : IIdGenerator
{
    private readonly SequentialGuidValueGenerator _generator = new();

    public Guid New() => _generator.Next(null!);
}
