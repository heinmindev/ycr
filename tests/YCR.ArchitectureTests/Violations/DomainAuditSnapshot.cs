using YCR.Application.Common.Abstractions;

namespace YCR.Domain.Network.Violations;

/// <summary>
/// A snapshot record sitting in a <c>YCR.Domain</c> namespace. Proves the rule rejects a payload
/// that has drifted into the domain, which is the mistake the placement half exists to catch: a
/// snapshot there would start being treated as a domain concept and would change shape with the
/// aggregate (hein's ruling, 2026-09-20).
/// </summary>
public sealed record DomainAuditSnapshot(string Code) : IAuditSnapshot;
