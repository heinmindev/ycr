namespace YCR.Application.Network.CreateStation;

/// <summary>Creates a station (spec S1).</summary>
public sealed record CreateStationCommand(string Code, string NameEn, string NameMy);
