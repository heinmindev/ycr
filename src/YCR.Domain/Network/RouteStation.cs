namespace YCR.Domain.Network;

/// <summary>
/// One station at one position in a <see cref="Route"/>'s sequence (F-003 spec §7
/// <c>network.RouteStations</c>).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-24; E5, E6): the station is referenced by
/// <see cref="StationId"/> only. There is deliberately no <see cref="Station"/> navigation, so a
/// route never loads, locks or changes a station; the database foreign key is the authority that
/// the station exists (R16). <see cref="Position"/> is a 1-based contiguous ordinal within the
/// route (R18). It is neither the ADR-0014 station short index nor the station code.
/// <para>
/// Rows are insert-only (R9, R10): nothing here can change after creation.
/// </para>
/// </remarks>
public sealed class RouteStation
{
    private RouteStation()
    {
    }

    public Guid RouteId { get; private set; }

    public int Position { get; private set; }

    public Guid StationId { get; private set; }

    internal static RouteStation Create(Guid routeId, int position, Guid stationId) =>
        new() { RouteId = routeId, Position = position, StationId = stationId };
}
