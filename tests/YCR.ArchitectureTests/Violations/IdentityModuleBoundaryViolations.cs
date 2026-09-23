namespace YCR.Application.Identity.Violations
{
    /// <summary>ADR-0012: the Identity module reaching into Network's context.</summary>
    public sealed class IdentityUsingNetworkContext
    {
        public YCR.Application.Network.INetworkDbContext Context { get; } = null!;
    }
}

namespace YCR.Application.Network.Violations
{
    /// <summary>ADR-0012: the Network module reaching into Identity's context.</summary>
    public sealed class NetworkUsingIdentityContext
    {
        public YCR.Application.Identity.IIdentityDbContext Context { get; } = null!;
    }
}
