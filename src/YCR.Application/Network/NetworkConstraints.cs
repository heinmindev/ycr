namespace YCR.Application.Network;

/// <summary>
/// Database constraint names the Network module's handlers recognise.
/// </summary>
/// <remarks>
/// A handler catches <see cref="Common.UniqueConstraintViolationException"/> and must match on
/// the constraint name, so the name has to be stated somewhere Application can see. The index
/// itself is declared by <c>StationConfiguration</c> in Infrastructure, which Application may not
/// reference.
/// <para>
/// The two are kept honest by <c>StationModelTests</c>, which asserts the mapped index name
/// equals this constant. Without that assertion a rename in Infrastructure would turn a
/// <c>409 Conflict</c> into an unhandled <c>500</c>, and nothing would fail until a duplicate
/// code was posted in production.
/// </para>
/// <para>
/// The route names are kept honest the same way, by <c>RouteModelTests</c>.
/// </para>
/// </remarks>
public static class NetworkConstraints
{
    public const string StationCodeUniqueIndex = "UX_Stations_Code";

    /// <summary>R14: route codes are unique across all routes, inactive ones included.</summary>
    public const string RouteCodeUniqueIndex = "UX_Routes_Code";

    /// <summary>
    /// R7: a station appears at most once in one route. On <c>(StationId, RouteId)</c>, so it also
    /// covers the station foreign key and EF adds no <c>IX_RouteStations_StationId</c>
    /// (spec Amendment 1, hein, 2026-09-24).
    /// </summary>
    public const string RouteStationUniqueIndex = "UX_RouteStations_StationId_RouteId";
}
