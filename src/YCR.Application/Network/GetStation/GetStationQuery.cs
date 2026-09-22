namespace YCR.Application.Network.GetStation;

/// <summary>Reads one station by id (spec S4, S8).</summary>
public sealed record GetStationQuery(Guid StationId);
