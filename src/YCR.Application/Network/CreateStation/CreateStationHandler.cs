using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;
using YCR.Application.Common.Abstractions;
using YCR.Domain.Common;
using YCR.Domain.Network;

namespace YCR.Application.Network.CreateStation;

/// <summary>
/// Creates a station, rejecting a code that already exists (spec S1, S5, S6, S13).
/// </summary>
/// <remarks>
/// Two things guard the code's uniqueness, and they are not redundant. The pre-check gives an
/// ordinary caller a clean <c>409</c> with a useful message. The unique index is the actual
/// authority: between the pre-check and the save, a concurrent request can insert the same code,
/// and only the database can settle that race (spec S13, R7).
/// <para>
/// The catch matches <em>one named constraint</em>. Any other unique-constraint violation is not
/// this conflict and propagates as an unexpected failure rather than being reported to the caller
/// as a duplicate station code (tech-lead ruling, plan §Unique-constraint translation).
/// </para>
/// </remarks>
public sealed class CreateStationHandler(
    INetworkDbContext db,
    IIdGenerator ids,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Result<Guid>> Handle(CreateStationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var code = StationCode.Create(command.Code);
        if (code.IsFailure)
        {
            return code.Error;
        }

        var name = BilingualName.Create(command.NameEn, command.NameMy);
        if (name.IsFailure)
        {
            return name.Error;
        }

        // Deactivated stations keep their rows, so this also enforces R3's "codes are never
        // reused" without a separate mechanism (spec S6).
        if (await db.Stations.AnyAsync(station => station.Code == code.Value, cancellationToken))
        {
            return NetworkErrors.StationCodeAlreadyExists(command.Code);
        }

        var station = Station.Create(ids.New(), code.Value, name.Value, clock.GetUtcNow());
        db.Stations.Add(station);

        audit.Record(
            "Network.StationCreated",
            NetworkAuditSubjects.Station,
            station.Id,
            before: null,
            after: StationAuditSnapshot.From(station));

        try
        {
            // One save commits the station and its audit row together (ADR-0004, ADR-0017).
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException violation)
            when (violation.ConstraintName == NetworkConstraints.StationCodeUniqueIndex)
        {
            return NetworkErrors.StationCodeAlreadyExists(command.Code);
        }

        return station.Id;
    }
}
