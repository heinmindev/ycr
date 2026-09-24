using System.Text.RegularExpressions;
using YCR.Domain.Common;

namespace YCR.Domain.Identity;

/// <summary>
/// A staff member's username — the staff identifier (spec R20).
/// </summary>
/// <remarks>
/// BUSINESS DECISION — provisional tech-lead ruling (hein, 2026-09-23; OQ34/D9) — not a Myanma
/// Railways answer: 3–50 characters of lower-case <c>a-z</c>, <c>0-9</c> and <c>.</c>. Nothing is
/// trimmed or case-folded: a value that is not already in that form is refused, not repaired.
/// </remarks>
public sealed record UserName
{
    private static readonly Regex ValidPattern = new(
        "^[a-z0-9.]{3,50}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private UserName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<UserName> Create(string? value) =>
        value is not null && ValidPattern.IsMatch(value)
            ? new UserName(value)
            : IdentityErrors.InvalidUserName;

    internal static UserName From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new UserName(value);
    }

    public override string ToString() => Value;
}
