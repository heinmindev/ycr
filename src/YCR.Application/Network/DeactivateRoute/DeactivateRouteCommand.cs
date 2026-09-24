namespace YCR.Application.Network.DeactivateRoute;

/// <summary>Deactivates a route (F-003 S12, S23).</summary>
public sealed record DeactivateRouteCommand(Guid RouteId);
