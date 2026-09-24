using Microsoft.EntityFrameworkCore;
using YCR.Domain.Network;

namespace YCR.Application.Network;

public interface INetworkDbContext
{
    DbSet<Station> Stations { get; }

    /// <summary>
    /// Routes and, through the aggregate, their <c>RouteStation</c> rows. There is deliberately no
    /// <c>DbSet&lt;RouteStation&gt;</c>: a sequence row is reached only through its route (F-003 plan P1).
    /// </summary>
    DbSet<Route> Routes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
