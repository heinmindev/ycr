namespace YCR.Application.Network.Contracts.Violations
{
    /// <summary>ADR-0025 item 1: a contract exposing a Network domain type.</summary>
    public sealed class ContractExposingNetworkDomain
    {
        public YCR.Domain.Network.Route Route { get; } = null!;
    }

    /// <summary>ADR-0025 item 1: a contract exposing the module's context.</summary>
    public sealed class ContractExposingNetworkContext
    {
        public YCR.Application.Network.INetworkDbContext Context { get; } = null!;
    }

    /// <summary>ADR-0025 item 1: a contract leaking EF Core, which leaks the domain with it.</summary>
    public sealed class ContractExposingEntityFramework
    {
        public Microsoft.EntityFrameworkCore.DbSet<YCR.Domain.Network.Station> Stations { get; } = null!;
    }
}
