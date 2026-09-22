using Microsoft.EntityFrameworkCore;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.DeactivateStation;

/// <summary>
/// Deactivates a station (spec S2, S7, S8, S27).
/// </summary>
/// <remarks>
/// Ordinary on purpose. <c>IsActive</c> is an EF concurrency token
/// (plan §Deactivation concurrency), so EF emits
/// <c>UPDATE ... SET IsActive = 0 WHERE Id = @id AND IsActive = @original</c> and one
/// <c>SaveChangesAsync</c> is enough. No explicit transaction, no <c>ExecuteUpdateAsync</c>,
/// nothing that bypasses the change tracker for a later slice to copy incorrectly.
/// <para>
/// Under two concurrent requests the loser's <c>WHERE</c> matches zero rows and EF throws
/// <see cref="DbUpdateConcurrencyException"/>, which maps to the <em>same</em> error as finding
/// the station already inactive. The caller cannot tell the two apart, and should not: the
/// outcome is identical (spec S27).
/// </para>
/// <para>
/// The audit row is written before the save and therefore inside it, so the losing request's
/// audit event rolls back with its update — exactly one event per successful deactivation.
/// <see cref="Station.Deactivate"/> stays the authority for the business rule, so AGENTS.md
/// rule 3 holds and the domain tests keep their meaning.
/// </para>
/// </remarks>
public sealed class DeactivateStationHandler(INetworkDbContext db, IAuditWriter audit)
{
    public async Task<Result> Handle(DeactivateStationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var station = await db.Stations
            .FirstOrDefaultAsync(candidate => candidate.Id == command.StationId, cancellationToken);

        if (station is null)
        {
            return NetworkErrors.StationNotFound;
        }

        var before = StationAuditSnapshot.From(station);

        var deactivation = station.Deactivate();
        if (deactivation.IsFailure)
        {
            return deactivation.Error;
        }

        audit.Record(
            "Network.StationDeactivated",
            NetworkAuditSubjects.Station,
            station.Id,
            before: before,
            after: StationAuditSnapshot.From(station));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request deactivated it first. Same outcome, same error as S7.
            return NetworkErrors.StationAlreadyInactive;
        }

        return Result.Success();
    }
}
