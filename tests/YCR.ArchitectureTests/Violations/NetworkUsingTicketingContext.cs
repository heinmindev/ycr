namespace YCR.Application.Ticketing
{
    public interface ITicketingDbContext;
}

namespace YCR.Application.Network.Violations
{
    public sealed class NetworkUsingTicketingContext
    {
        public YCR.Application.Ticketing.ITicketingDbContext Context { get; } = null!;
    }
}
