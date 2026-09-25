namespace YCR.Application.Network.GetRoute;

/// <summary>
/// One position of a route, with the station's <em>current</em> code, names and active flag, read
/// at query time (F-003 R24). A route row stores only the station id.
/// </summary>
public sealed record RouteStationDto(
    int Position,
    Guid StationId,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive);
