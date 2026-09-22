namespace YCR.Application.Network.DeactivateStation;

/// <summary>Deactivates a station (spec S2, S7, S27).</summary>
public sealed record DeactivateStationCommand(Guid StationId);
