namespace YCR.Application.Timetable.Violations
{
    /// <summary>ADR-0012 item 6, ADR-0025: Timetable reaching into Network's domain.</summary>
    public sealed class TimetableUsingNetworkDomain
    {
        public YCR.Domain.Network.Route Route { get; } = null!;
    }

    /// <summary>ADR-0012 item 4, ADR-0025: Timetable reaching into Network's context.</summary>
    public sealed class TimetableUsingNetworkContext
    {
        public YCR.Application.Network.INetworkDbContext Context { get; } = null!;
    }

    /// <summary>Permitted (ADR-0025): Timetable using the Network contract and its records.</summary>
    public sealed class TimetableUsingNetworkContracts
    {
        public YCR.Application.Network.Contracts.INetworkReader Reader { get; } = null!;

        public YCR.Application.Network.Contracts.RouteReference? Route { get; }
    }
}
