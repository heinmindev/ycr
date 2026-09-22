using Microsoft.EntityFrameworkCore;
using YCR.Application.Network;
using YCR.Infrastructure.Persistence;

namespace YCR.Application.Network.Violations
{
    public sealed class ApplicationUsingInfrastructure
    {
        public YcrDbContext Context { get; } = null!;
    }
}

namespace YCR.Application.Reporting.Violations
{
    public sealed class ReportingUsingNetworkContext
    {
        public INetworkDbContext Context { get; } = null!;
    }
}

namespace YCR.Domain.Network.Violations
{
    public sealed class DomainUsingOtherModule
    {
        public YCR.Domain.Ticketing.Ticket Ticket { get; } = null!;
    }

    public sealed class DomainUsingApplication
    {
        public INetworkDbContext Context { get; } = null!;
    }

    public sealed class DomainUsingEntityFrameworkCore
    {
        public DbContext Context { get; } = null!;
    }
}

namespace YCR.Domain.Common.Violations
{
    public sealed class CommonUsingNetworkModule
    {
        public YCR.Domain.Network.Station Station { get; } = null!;
    }
}
