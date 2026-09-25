using YCR.Domain.Common;

namespace YCR.Domain.Network;

public static class NetworkErrors
{
    public static readonly Error InvalidStationCode = Error.Validation(
        "Network.InvalidStationCode",
        "The station code is invalid.");

    public static readonly Error InvalidStationName = Error.Validation(
        "Network.InvalidStationName",
        "Both station names must be valid.");

    public static readonly Error StationAlreadyInactive = Error.BusinessRule(
        "Network.StationAlreadyInactive",
        "Station is already inactive.");

    public static readonly Error StationNotFound = Error.NotFound(
        "Network.StationNotFound",
        "Station was not found.");

    public static Error StationCodeAlreadyExists(string code) => Error.Conflict(
        "Network.StationCodeAlreadyExists",
        $"Station code '{code}' already exists.");

    // Routes (F-003). Every message names only the caller's own input: a code or a station id.

    public static readonly Error InvalidRouteCode = Error.Validation(
        "Network.InvalidRouteCode",
        "The route code is invalid.");

    public static readonly Error InvalidRouteName = Error.Validation(
        "Network.InvalidRouteName",
        "Both route names must be valid.");

    public static readonly Error RouteNotFound = Error.NotFound(
        "Network.RouteNotFound",
        "Route was not found.");

    public static readonly Error RouteAlreadyInactive = Error.BusinessRule(
        "Network.RouteAlreadyInactive",
        "Route is already inactive.");

    /// <summary>R8: at least 2 stations for an open route, 3 for a closed one.</summary>
    public static readonly Error RouteTooFewStations = Error.BusinessRule(
        "Network.RouteTooFewStations",
        "An open route needs at least 2 stations and a closed route at least 3.");

    public static Error RouteCodeAlreadyExists(string code) => Error.Conflict(
        "Network.RouteCodeAlreadyExists",
        $"Route code '{code}' already exists.");

    /// <summary>
    /// R16. A <c>422</c>, not a <c>404</c> (E8): the addressed resource is the new route, and the
    /// request is well-formed; it names a station that does not exist.
    /// </summary>
    public static Error RouteStationNotFound(Guid stationId) => Error.BusinessRule(
        "Network.RouteStationNotFound",
        $"Station '{stationId}' was not found.");

    /// <summary>R11: an inactive station cannot be placed in a route.</summary>
    public static Error RouteStationInactive(Guid stationId) => Error.BusinessRule(
        "Network.RouteStationInactive",
        $"Station '{stationId}' is inactive.");

    /// <summary>R7: a station appears at most once in one route.</summary>
    public static Error RouteStationRepeated(Guid stationId) => Error.BusinessRule(
        "Network.RouteStationRepeated",
        $"Station '{stationId}' appears more than once in the sequence.");

    /// <summary>
    /// A page request outside the limits `docs/20` §4 fixes for every collection (spec S10).
    /// </summary>
    /// <remarks>
    /// The limits themselves are platform-wide and live in `YCR.Application.Common.Pagination`.
    /// Only the error code is per module, because `docs/20` §2 requires `&lt;Module&gt;.&lt;Reason&gt;`
    /// — which is also why every `Network.*` code stays discoverable in this one class.
    /// </remarks>
    public static readonly Error InvalidPageRequest = Error.Validation(
        "Network.InvalidPageRequest",
        "Page must be 1 or greater and pageSize must be between 1 and 200.");
}
