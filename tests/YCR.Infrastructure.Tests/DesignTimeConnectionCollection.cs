using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests;

/// <summary>
/// Serialises the test classes that mutate the process-wide
/// <see cref="YcrDbContextFactory.DesignTimeConnectionVariable"/>.
/// </summary>
/// <remarks>
/// T-022. Each such test saves the variable, sets it, and restores it in <c>finally</c>, which is
/// only sound if no other test changes it in between. xUnit runs classes in the same collection
/// one after another but runs different collections in parallel, so classes left in their default
/// per-class collections raced: one set the variable to null while another set it to a value,
/// and <c>CreateDbContext_WithoutDesignTimeConnection_ThrowsClearMessage</c> intermittently saw no
/// exception. Every class that calls <c>Environment.SetEnvironmentVariable</c> on this variable
/// belongs in this collection. Every other collection still runs in parallel.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class DesignTimeConnectionCollection
{
    public const string Name = "Design-time connection environment variable";
}
