using YCR.Domain.Common;

namespace YCR.Domain.Network;

/// <summary>Raised when a route is deactivated (F-003 R15). Collected, not dispatched (F-001 P6).</summary>
public sealed record RouteDeactivated(Guid RouteId) : IDomainEvent;
