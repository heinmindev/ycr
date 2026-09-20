using YCR.Domain.Common;

namespace YCR.Domain.Tests.Common;

public sealed class AggregateRootTests
{
    [Fact]
    public void Raise_CollectsDomainEventWithoutDispatchingIt()
    {
        var aggregate = new TestAggregate();
        var domainEvent = new TestEvent();

        aggregate.Record(domainEvent);

        var collected = Assert.Single(aggregate.DomainEvents);
        Assert.Same(domainEvent, collected);
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public void Record(IDomainEvent domainEvent) => Raise(domainEvent);
    }

    private sealed record TestEvent : IDomainEvent;
}
