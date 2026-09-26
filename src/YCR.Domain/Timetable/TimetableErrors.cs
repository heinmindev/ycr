using YCR.Domain.Common;

namespace YCR.Domain.Timetable;

/// <summary>
/// Every <c>Timetable.*</c> error code (F-004 spec §6; plan P22). Messages name only the caller's
/// own input: a code, a route id or a station id.
/// </summary>
public static class TimetableErrors
{
    /// <summary>R6: 2-10 characters of <c>A</c>-<c>Z</c> and <c>0</c>-<c>9</c> after trimming.</summary>
    public static readonly Error InvalidServiceCode = Error.Validation(
        "Timetable.InvalidServiceCode",
        "The service code is invalid.");

    /// <summary>R7: both names required, 1-100 characters each after trimming.</summary>
    public static readonly Error InvalidServiceName = Error.Validation(
        "Timetable.InvalidServiceName",
        "Both service names must be valid.");

    /// <summary>R19: at creation, <c>EffectiveTo</c> is not earlier than <c>EffectiveFrom</c>.</summary>
    public static readonly Error InvalidEffectivePeriod = Error.Validation(
        "Timetable.InvalidEffectivePeriod",
        "EffectiveTo must not be earlier than EffectiveFrom.");

    /// <summary>A page request outside the `docs/20` §4 limits (spec S44).</summary>
    public static readonly Error InvalidPageRequest = Error.Validation(
        "Timetable.InvalidPageRequest",
        "Page must be 1 or greater and pageSize must be between 1 and 200.");

    public static readonly Error ServiceNotFound = Error.NotFound(
        "Timetable.ServiceNotFound",
        "Service was not found.");

    /// <summary>R35: two services with one code may not have overlapping periods.</summary>
    public static Error ServiceCodePeriodOverlap(string code) => Error.Conflict(
        "Timetable.ServiceCodePeriodOverlap",
        $"Another service with code '{code}' has an overlapping effective period.");

    /// <summary>R36: the concurrency-token backstop behind the code lock.</summary>
    public static readonly Error ServiceChangedConcurrently = Error.Conflict(
        "Timetable.ServiceChangedConcurrently",
        "The service was changed by another request. Read it again and retry.");

    /// <summary>R39: <c>EffectiveTo</c>, if given, is not earlier than today (Asia/Yangon).</summary>
    public static readonly Error ServiceEffectiveToInPast = Error.BusinessRule(
        "Timetable.ServiceEffectiveToInPast",
        "EffectiveTo must not be earlier than today.");

    /// <summary>
    /// R15. A <c>422</c>, not a <c>404</c> (F-003 E8 pattern): the addressed resource is the new
    /// service, and the request names a route that does not exist.
    /// </summary>
    public static Error ServiceRouteNotFound(Guid routeId) => Error.BusinessRule(
        "Timetable.ServiceRouteNotFound",
        $"Route '{routeId}' was not found.");

    /// <summary>R15: a service cannot be created on an inactive route.</summary>
    public static Error ServiceRouteInactive(Guid routeId) => Error.BusinessRule(
        "Timetable.ServiceRouteInactive",
        $"Route '{routeId}' is inactive.");

    /// <summary>R11: every stop is a station of the service's route.</summary>
    public static Error ServiceStopNotOnRoute(Guid stationId) => Error.BusinessRule(
        "Timetable.ServiceStopNotOnRoute",
        $"Station '{stationId}' is not on the route.");

    /// <summary>R14: no station appears twice among the stops, except the full-circuit closure.</summary>
    public static Error ServiceStopRepeated(Guid stationId) => Error.BusinessRule(
        "Timetable.ServiceStopRepeated",
        $"Station '{stationId}' appears more than once among the stops.");

    /// <summary>R13, R14: at least 2 stops, or 3 distinct stations for a full circuit.</summary>
    public static readonly Error ServiceTooFewStops = Error.BusinessRule(
        "Timetable.ServiceTooFewStops",
        "A service needs at least 2 stops, and a full circuit at least 3 distinct stations.");

    /// <summary>R12: stops follow the route order in the service's direction.</summary>
    public static Error ServiceStopsOutOfOrder(Guid stationId) => Error.BusinessRule(
        "Timetable.ServiceStopsOutOfOrder",
        $"Station '{stationId}' is out of order for the route and direction.");

    /// <summary>R15: a service cannot stop at an inactive station.</summary>
    public static Error ServiceStopStationInactive(Guid stationId) => Error.BusinessRule(
        "Timetable.ServiceStopStationInactive",
        $"Station '{stationId}' is inactive.");

    /// <summary>R21: the withdrawal date is not earlier than today (Asia/Yangon).</summary>
    public static readonly Error WithdrawalDateInPast = Error.BusinessRule(
        "Timetable.WithdrawalDateInPast",
        "The withdrawal date must not be earlier than today.");

    /// <summary>R21: a withdrawal must shorten the period; it never extends or keeps it.</summary>
    public static readonly Error WithdrawalDoesNotShorten = Error.BusinessRule(
        "Timetable.WithdrawalDoesNotShorten",
        "The withdrawal would not shorten the service's effective period.");
}
