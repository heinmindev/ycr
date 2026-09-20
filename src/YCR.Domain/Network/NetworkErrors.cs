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
}
