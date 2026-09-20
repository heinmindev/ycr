using YCR.Application.Common.Abstractions;

namespace YCR.ArchitectureTests.Violations;

/// <summary>
/// A snapshot that is a class rather than a record. Proves the "must be a record" half of the
/// rule has teeth (hein's ruling, 2026-09-20).
/// </summary>
public sealed class NonRecordAuditSnapshot : IAuditSnapshot
{
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// A snapshot record in the wrong namespace — <c>YCR.ArchitectureTests.Violations</c> is neither
/// <c>YCR.Application.&lt;Module&gt;</c> nor a module namespace at all. Proves the placement half
/// of the rule has teeth.
/// </summary>
public sealed record MisplacedAuditSnapshot(string Code) : IAuditSnapshot;
