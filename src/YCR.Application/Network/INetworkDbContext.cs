using Microsoft.EntityFrameworkCore;
using YCR.Domain.Network;

namespace YCR.Application.Network;

public interface INetworkDbContext
{
    DbSet<Station> Stations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
