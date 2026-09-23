using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace YCR.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Shared mapping rules for the <c>identity</c> schema.</summary>
internal static class IdentityConfiguration
{
    /// <summary>Every <c>*Utc</c> column is <c>datetimeoffset(3)</c> (<c>docs/20</c> §2, <c>docs/07</c>).</summary>
    public static PropertyBuilder<T> Utc<T>(PropertyBuilder<T> property) =>
        property.HasColumnType("datetimeoffset(3)");

    /// <summary>
    /// <c>CK_&lt;Table&gt;_&lt;Column&gt;_Utc</c>: the offset is zero (ADR-0018). A null value
    /// passes, as a check constraint's unknown result does.
    /// </summary>
    public static void UtcCheck(TableBuilder table, string tableName, string column) =>
        table.HasCheckConstraint($"CK_{tableName}_{column}_Utc", $"DATEPART(TZOFFSET, [{column}]) = 0");
}
