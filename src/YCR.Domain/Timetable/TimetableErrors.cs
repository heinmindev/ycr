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

    // ----- F-005 timetable versions (spec §6; plan P19): 21 codes -----

    /// <summary>F-005 R7: both names required, 1-100 characters each after trimming.</summary>
    public static readonly Error InvalidScheduleVersionName = Error.Validation(
        "Timetable.InvalidScheduleVersionName",
        "Both schedule version names must be valid.");

    /// <summary>F-005 R16 (ADR-0027): a time is exactly <c>HH:mm</c>, 00:00-23:59.</summary>
    public static readonly Error InvalidTimetableTime = Error.Validation(
        "Timetable.InvalidTimetableTime",
        "A timetable time must be HH:mm, from 00:00 to 23:59.");

    public static readonly Error ScheduleVersionNotFound = Error.NotFound(
        "Timetable.ScheduleVersionNotFound",
        "Schedule version was not found.");

    /// <summary>F-005 spec §6: the version does not list the service.</summary>
    public static readonly Error ScheduleServiceNotInVersion = Error.NotFound(
        "Timetable.ScheduleServiceNotInVersion",
        "The schedule version does not list the service.");

    /// <summary>F-005 R21: no published version starts on or before the date.</summary>
    public static readonly Error ScheduleVersionNotInForce = Error.NotFound(
        "Timetable.ScheduleVersionNotInForce",
        "No schedule version is in force on the date.");

    /// <summary>F-005 R22: another published version has the same start date.</summary>
    public static readonly Error ScheduleVersionEffectiveFromTaken = Error.Conflict(
        "Timetable.ScheduleVersionEffectiveFromTaken",
        "Another published schedule version starts on the same date.");

    /// <summary>F-005 R47: the concurrency-token backstop behind the Timetable-wide lock.</summary>
    public static readonly Error ScheduleVersionChangedConcurrently = Error.Conflict(
        "Timetable.ScheduleVersionChangedConcurrently",
        "The schedule version was changed by another request. Read it again and retry.");

    /// <summary>F-005 R31: the start date is not earlier than today (Asia/Yangon).</summary>
    public static readonly Error ScheduleVersionEffectiveFromInPast = Error.BusinessRule(
        "Timetable.ScheduleVersionEffectiveFromInPast",
        "The schedule version's start date must not be earlier than today.");

    /// <summary>F-005 R50 (Amendments 1-2): a version listing no services starts after today.</summary>
    public static readonly Error EmptyScheduleVersionNotInFuture = Error.BusinessRule(
        "Timetable.EmptyScheduleVersionNotInFuture",
        "A schedule version that lists no services must start later than today.");

    /// <summary>
    /// F-005 R17. A <c>422</c>, not a <c>404</c> (F-003 E8 pattern): the addressed resource is the
    /// new version, and the request names a service that does not exist.
    /// </summary>
    public static Error ScheduleServiceNotFound(Guid serviceId) => Error.BusinessRule(
        "Timetable.ScheduleServiceNotFound",
        $"Service '{serviceId}' was not found.");

    /// <summary>F-005 R17: a service is listed once.</summary>
    public static Error ScheduleServiceRepeated(Guid serviceId) => Error.BusinessRule(
        "Timetable.ScheduleServiceRepeated",
        $"Service '{serviceId}' is listed more than once.");

    /// <summary>F-005 R17: a listed service is effective, and not withdrawn, on the start date.</summary>
    public static Error ScheduleServiceNotEffective(Guid serviceId) => Error.BusinessRule(
        "Timetable.ScheduleServiceNotEffective",
        $"Service '{serviceId}' is not effective on the schedule version's start date.");

    /// <summary>F-005 R10: a position greater than the service's stop count.</summary>
    public static Error ScheduleStopNotInService(Guid serviceId, int position) => Error.BusinessRule(
        "Timetable.ScheduleStopNotInService",
        $"Service '{serviceId}' has no stop {position}.");

    /// <summary>F-005 R10: a stop position missing or repeated, or a required time missing.</summary>
    public static Error ScheduleStopTimesIncomplete(Guid serviceId, int position) => Error.BusinessRule(
        "Timetable.ScheduleStopTimesIncomplete",
        $"Service '{serviceId}' stop {position} is missing, repeated or lacks a required time.");

    /// <summary>F-005 R10: an arrival at the first stop or a departure at the last stop.</summary>
    public static Error ScheduleStopTimeUnexpected(Guid serviceId, int position) => Error.BusinessRule(
        "Timetable.ScheduleStopTimeUnexpected",
        $"Service '{serviceId}' stop {position} has a time where none is allowed.");

    /// <summary>F-005 R12: at a stop, the departure is not earlier than the arrival.</summary>
    public static Error ScheduleDwellNegative(Guid serviceId, int position) => Error.BusinessRule(
        "Timetable.ScheduleDwellNegative",
        $"Service '{serviceId}' stop {position} departs before it arrives.");

    /// <summary>F-005 R12, R15: each arrival is later than the previous stop's departure.</summary>
    public static Error ScheduleTimesNotIncreasing(Guid serviceId, int position) => Error.BusinessRule(
        "Timetable.ScheduleTimesNotIncreasing",
        $"Service '{serviceId}' stop {position} arrives no later than the previous stop departs.");

    /// <summary>F-005 R28, R30: only a draft can be published or discarded.</summary>
    public static readonly Error ScheduleVersionNotDraft = Error.BusinessRule(
        "Timetable.ScheduleVersionNotDraft",
        "The schedule version is not a draft.");

    /// <summary>F-005 R34: only a published version can be cancelled.</summary>
    public static readonly Error ScheduleVersionNotPublished = Error.BusinessRule(
        "Timetable.ScheduleVersionNotPublished",
        "The schedule version is not published.");

    /// <summary>F-005 R34: a version is cancelled only while its start date is later than today.</summary>
    public static readonly Error ScheduleVersionAlreadyEffective = Error.BusinessRule(
        "Timetable.ScheduleVersionAlreadyEffective",
        "The schedule version has already taken effect.");

    /// <summary>
    /// F-005 R19 (changes F-004 R21): a service cannot be withdrawn from a date while a published
    /// version that lists it applies on or after that date.
    /// </summary>
    public static Error ServiceInPublishedScheduleVersion(Guid serviceId) => Error.BusinessRule(
        "Timetable.ServiceInPublishedScheduleVersion",
        $"Service '{serviceId}' is listed by a published schedule version that applies on or after the withdrawal date.");
}
