using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using YCR.Api.Common.Authorization;
using YCR.Application.Common.Abstractions;

namespace YCR.Api.Common;

/// <summary>
/// Supplies the authenticated request context from <see cref="HttpContext"/>.
/// </summary>
/// <remarks>
/// REQUIRED CONTROL (ADR-0017 item 2): every value here is derived server-side. The user id and
/// roles come from the validated principal, the address from the connection as the server
/// observed it, the correlation id from the ambient activity, and the permission from the
/// endpoint's own authorization metadata. <strong>Nothing is read from the request body, the
/// query string or a client-supplied header</strong>, so a caller cannot influence what the
/// audit ledger records about them.
/// <para>
/// This type is a plan addition: the plan's `src/YCR.Api` inventory lists no
/// <c>ICurrentUser</c> implementation, but <c>AuditWriter</c> requires one and the composition
/// root is the only place with an <c>HttpContext</c>. Recorded in `progress.md`.
/// </para>
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public IReadOnlyCollection<string> Roles =>
        Principal is null
            ? []
            : [.. Principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value)];

    /// <summary>
    /// The peer address of the TCP connection, never <c>X-Forwarded-For</c>.
    /// </summary>
    /// <remarks>
    /// A forwarded header is client-supplied and therefore forgeable, which ADR-0017 item 2
    /// rules out for an audit field. When this platform is deployed behind a proxy, the correct
    /// fix is <c>ForwardedHeadersOptions</c> with an explicit list of trusted proxies — a
    /// deployment decision, not something to assume here.
    /// </remarks>
    public string? ClientIp => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string CorrelationId =>
        Activity.Current?.Id
        ?? accessor.HttpContext?.TraceIdentifier
        ?? string.Empty;

    /// <summary>
    /// The permission the matched endpoint required, read from its authorization metadata.
    /// </summary>
    /// <remarks>
    /// Derived, not passed (hein's ruling, 2026-09-20). A handler that named its own authority
    /// could claim one it never held, and the claim would be unfalsifiable once written to an
    /// append-only row. Because <see cref="PermissionPolicyProvider"/> names each policy after
    /// the permission it enforces, the endpoint's policy name <em>is</em> the permission.
    /// </remarks>
    public string? AuthorizedByPermission =>
        accessor.HttpContext?.GetEndpoint()?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy)
            .FirstOrDefault(policy => !string.IsNullOrEmpty(policy));

    private ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;
}
