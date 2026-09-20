using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.GetStation;

/// <summary>Reads one station (spec S4, S8).</summary>
/// <remarks>
/// ADR-0004: queries read with <c>AsNoTracking()</c>. The entity is materialised and then mapped
/// rather than projected in SQL, because <c>Station.Code</c> is a value object behind an EF value
/// converter and EF cannot translate member access through one — <c>station.Code.Value</c> inside
/// a <c>Select</c> does not compile to SQL. A single row by primary key makes the difference
/// immaterial, and the mapping stays in one place (<see cref="StationDto.From"/>).
/// </remarks>
public sealed class GetStationHandler(INetworkDbContext db)
{
    public async Task<Result<StationDto>> Handle(GetStationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var station = await db.Stations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.StationId, cancellationToken);

        return station is null
            ? NetworkErrors.StationNotFound
            : StationDto.From(station);
    }
}
