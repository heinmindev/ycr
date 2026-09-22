using YCR.Domain.Common;

namespace YCR.Api.Violations;

public sealed class ApiUsingAggregateRoot
{
    public AggregateRoot Aggregate { get; } = null!;
}
