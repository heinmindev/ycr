namespace YCR.Application.Network.GetRoute;

/// <summary>Reads one route with its sequence (F-003 S2, S12).</summary>
public sealed record GetRouteQuery(Guid RouteId);
