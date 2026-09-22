using Microsoft.EntityFrameworkCore;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.GetStation;

/// <summary>Reads one station (spec S4, S8).</summary>
/// <remarks>
/// ADR-0004 and `docs/20` §4: the query projects, and reads with <c>AsNoTracking()</c>. It
/// projects whole value-object properties — see <see cref="StationProjection"/> for why reaching
/// inside them does not translate — so the entity is never materialised and no column beyond the
/// projection is fetched.
/// </remarks>
public sealed class GetStationHandler(INetworkDbContext db)
{
    public async Task<Result<StationDto>> Handle(GetStationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var projection = await db.Stations
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.StationId)
            .Select(candidate => new StationProjection(
                candidate.Id,
                candidate.Code,
                candidate.Name.En,
                candidate.Name.My,
                candidate.IsActive,
                candidate.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return projection is null
            ? NetworkErrors.StationNotFound
            : projection.ToDto();
    }
}
