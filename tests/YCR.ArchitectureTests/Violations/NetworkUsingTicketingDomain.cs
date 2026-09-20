namespace YCR.Domain.Ticketing
{
    public sealed class Ticket;
}

namespace YCR.Application.Network.Violations
{
    public sealed class NetworkUsingTicketingDomain
    {
        public YCR.Domain.Ticketing.Ticket Ticket { get; } = null!;
    }
}
