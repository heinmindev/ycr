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
