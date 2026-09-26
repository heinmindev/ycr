using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Abstractions;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// A decorator over the real <see cref="IAuditWriter"/> that runs a callback after each
/// <see cref="IAuditWriter.Record"/>. A handler records its audit event after it has loaded and
/// decided, and before its one save, so the callback lands exactly between the two (F-004 plan
/// §R35, the R36 backstop test).
/// </summary>
public sealed class AuditRecordHook(IAuditWriter inner, Action<string> afterRecord) : IAuditWriter
{
    public void Record(
        string action,
        string subjectType,
        Guid? subjectId,
        IAuditSnapshot? before,
        IAuditSnapshot? after,
        string? reasonCode = null)
    {
        inner.Record(action, subjectType, subjectId, before, after, reasonCode);
        afterRecord(action);
    }

    public void RecordWithoutActor(
        string action,
        string subjectType,
        Guid? subjectId,
        IAuditSnapshot? before,
        IAuditSnapshot? after) =>
        inner.RecordWithoutActor(action, subjectType, subjectId, before, after);

    public void RecordSignIn(
        Guid verifiedUserId,
        IReadOnlyCollection<string> roles,
        string action,
        string subjectType,
        IAuditSnapshot? after) =>
        inner.RecordSignIn(verifiedUserId, roles, action, subjectType, after);

    /// <summary>Wraps whatever <c>AddInfrastructure</c> registered, so the real writer still runs.</summary>
    public static void Register(IServiceCollection services, Action<string> afterRecord)
    {
        var real = services.Last(descriptor => descriptor.ServiceType == typeof(IAuditWriter));
        var implementation = real.ImplementationType
            ?? throw new InvalidOperationException("The real IAuditWriter is expected to be registered by type.");
        services.Remove(real);
        services.AddScoped<IAuditWriter>(provider => new AuditRecordHook(
            (IAuditWriter)ActivatorUtilities.CreateInstance(provider, implementation),
            afterRecord));
    }
}
