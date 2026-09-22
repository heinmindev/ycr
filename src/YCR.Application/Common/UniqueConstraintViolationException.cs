namespace YCR.Application.Common;

/// <summary>
/// A unique constraint was violated, named by the database constraint that rejected the write.
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, 2026-09-20; plan §Unique-constraint translation): this
/// exists so a handler can recognise a specific uniqueness failure without catching a
/// provider-specific exception, which would put a SQL Server dependency in Application and
/// break ADR-0004's provider-neutral boundary. Infrastructure translates SQL Server errors
/// 2601 and 2627 into this type and supplies <see cref="ConstraintName"/>.
/// <para>
/// A handler must match on <see cref="ConstraintName"/> and let anything else propagate.
/// Catching this type without checking the name would report an unrelated uniqueness failure
/// as whatever conflict that handler happens to know about — which is the precise mistake the
/// ruling was made to prevent.
/// </para>
/// </remarks>
public sealed class UniqueConstraintViolationException(string constraintName, Exception innerException)
    : Exception($"A unique constraint was violated: '{constraintName}'.", innerException)
{
    /// <summary>The database index or constraint that rejected the write, e.g. <c>UX_Stations_Code</c>.</summary>
    public string ConstraintName { get; } = constraintName;
}
