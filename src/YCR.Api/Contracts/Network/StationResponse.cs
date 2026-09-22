using YCR.Application.Network.GetStation;

namespace YCR.Api.Contracts.Network;

/// <summary>A station as the API returns it (docs/20 §2).</summary>
/// <remarks>
/// Plan P5 keeps this separate from <c>StationDto</c>. They match today, and that is the point:
/// this one is the versioned public contract, the other is internal, and collapsing them would
/// turn an Application refactor into a breaking API change.
/// <para>
/// AGENTS.md rule 4: an EF entity never leaves through the API. The entity cannot even be
/// referenced here — plan P11's architecture test forbids <c>YCR.Api</c> from depending on any
/// <c>YCR.Domain.&lt;Module&gt;</c> type.
/// </para>
/// </remarks>
public sealed record StationResponse(
    Guid Id,
    string Code,
    string NameEn,
    string NameMy,
    bool IsActive,
    DateTimeOffset CreatedAtUtc)
{
    public static StationResponse From(StationDto station)
    {
        ArgumentNullException.ThrowIfNull(station);

        return new StationResponse(
            station.Id,
            station.Code,
            station.NameEn,
            station.NameMy,
            station.IsActive,
            station.CreatedAtUtc);
    }
}

/// <summary>The body of a successful <c>POST /api/v1/stations</c>.</summary>
public sealed record CreateStationResponse(Guid Id);

/// <summary>The paged envelope docs/20 §4 fixes for every collection.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
