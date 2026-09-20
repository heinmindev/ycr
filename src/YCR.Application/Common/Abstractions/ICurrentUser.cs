namespace YCR.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    IReadOnlyCollection<string> Roles { get; }

    string? ClientIp { get; }

    string CorrelationId { get; }
}
