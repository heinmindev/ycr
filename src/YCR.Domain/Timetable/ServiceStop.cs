namespace YCR.Domain.Timetable;

/// <summary>
/// One stop of a <see cref="Service"/> (F-004 spec §7 <c>timetable.ServiceStops</c>; plan P1).
/// </summary>
/// <remarks>
/// The station is referenced by <see cref="StationId"/> only, with no navigation, because it
/// belongs to the Network module (R8, R29; ADR-0025): the cross-schema foreign key is the database
/// authority that it exists. <see cref="Position"/> is the 1-based place in the service's stop list,
/// not the route position and not the ADR-0014 short index. There are no times and no stop
/// attributes (R14, R22). Rows are insert-only (R20).
/// </remarks>
public sealed class ServiceStop
{
    private ServiceStop()
    {
    }

    public Guid ServiceId { get; private set; }

    public int Position { get; private set; }

    public Guid StationId { get; private set; }

    internal static ServiceStop Create(Guid serviceId, int position, Guid stationId) =>
        new() { ServiceId = serviceId, Position = position, StationId = stationId };
}
