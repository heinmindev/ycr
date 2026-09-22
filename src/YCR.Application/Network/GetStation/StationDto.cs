using YCR.Domain.Network;

namespace YCR.Application.Network.GetStation;

/// <summary>
/// A station as the Application layer hands it out.
/// </summary>
/// <remarks>
/// Plan P5 keeps this separate from the API's <c>StationResponse</c>. They look alike today and
/// that is fine: the API contract is versioned and public, this one is internal, and collapsing
/// them would make an Application refactor a breaking API change.
/// <para>
/// AGENTS.md rule 4: no EF entity ever leaves through an API, and keeping the mapping here means
/// no endpoint has to remember that.
/// </para>
/// </remarks>
public sealed record StationDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public static StationDto From(Station station)
    {
        ArgumentNullException.ThrowIfNull(station);

        return new StationDto(
            station.Id,
            station.Code.Value,
            station.Name.En,
            station.Name.My,
            station.IsActive,
            station.CreatedAtUtc);
    }
}
