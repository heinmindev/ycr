using System.Diagnostics;
using YCR.Application.Common.Abstractions;

namespace YCR.Worker;

/// <summary>
/// The <see cref="ICurrentUser"/> of a process that serves no request: no user, no role, no
/// client address, no permission (plan P8; D10; ADR-0017 §2). Audit rows it writes therefore carry
/// null actor fields, which is what the bootstrap's <c>Identity.UserCreated</c> requires (S32).
/// </summary>
/// <remarks>
/// One correlation id per instance, from the ambient activity when there is one, so every row one
/// run writes can be found together.
/// </remarks>
public sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public IReadOnlyCollection<string> Roles { get; } = [];

    public string? ClientIp => null;

    public string CorrelationId { get; } = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

    public string? AuthorizedByPermission => null;
}
