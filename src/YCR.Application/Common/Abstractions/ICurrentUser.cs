namespace YCR.Application.Common.Abstractions;

/// <summary>
/// The authenticated server-side context for the current request.
/// </summary>
/// <remarks>
/// ADR-0017 item 2: every actor field in the audit ledger comes from here and from the server's
/// view of the connection, never from a request body or header.
/// </remarks>
public interface ICurrentUser
{
    Guid? UserId { get; }

    IReadOnlyCollection<string> Roles { get; }

    string? ClientIp { get; }

    string CorrelationId { get; }

    /// <summary>
    /// The permission that authorized the current request, e.g. <c>stations.manage</c>.
    /// </summary>
    /// <remarks>
    /// BUSINESS DECISION (hein, 2026-09-20): derived server-side from the endpoint's required
    /// permission, not passed by a handler. A handler that named its own authority could claim
    /// one it never held, and the claim would be unfalsifiable once written to an append-only
    /// row. Null for unauthenticated or system-initiated work.
    /// </remarks>
    string? AuthorizedByPermission { get; }
}
