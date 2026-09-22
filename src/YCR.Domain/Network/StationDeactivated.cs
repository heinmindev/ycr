using YCR.Domain.Common;

namespace YCR.Domain.Network;

public sealed record StationDeactivated(Guid StationId) : IDomainEvent;
