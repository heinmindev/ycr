using YCR.Domain.Common;

namespace YCR.Domain.Network;

public sealed class Station : AggregateRoot
{
    private Station()
    {
    }

    public StationCode Code { get; private set; } = null!;

    public BilingualName Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Station Create(Guid id, StationCode code, BilingualName name, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("CreatedAtUtc must use the UTC offset.", nameof(nowUtc));
        }

        return new Station
        {
            Id = id,
            Code = code,
            Name = name,
            IsActive = true,
            CreatedAtUtc = nowUtc
        };
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return NetworkErrors.StationAlreadyInactive;
        }

        IsActive = false;
        Raise(new StationDeactivated(Id));
        return Result.Success();
    }
}
