using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Timetable;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// Records, across both lock abstractions, the order in which one scope requests its locks
/// (F-005 plan P9: the service-code lock, then the Timetable-wide lock). The real locks still run.
/// </summary>
public sealed class RecordingLocks
{
    public const string ScheduleVersions = "timetable.ScheduleVersions";

    private readonly ConcurrentQueue<string> _requests = new();

    /// <summary>The locks requested, in order: <c>timetable.ServiceCode:&lt;code&gt;</c> or <see cref="ScheduleVersions"/>.</summary>
    public IReadOnlyList<string> Requests => [.. _requests];

    /// <summary>Wraps both registered locks, keeping the real ones underneath.</summary>
    public void Register(IServiceCollection services)
    {
        var codeLock = services.Last(descriptor => descriptor.ServiceType == typeof(IServiceCodeLock));
        var scheduleLock = services.Last(descriptor => descriptor.ServiceType == typeof(IScheduleVersionsLock));
        var codeImplementation = codeLock.ImplementationType
            ?? throw new InvalidOperationException("The real IServiceCodeLock is expected to be registered by type.");
        var scheduleImplementation = scheduleLock.ImplementationType
            ?? throw new InvalidOperationException("The real IScheduleVersionsLock is expected to be registered by type.");
        services.Remove(codeLock);
        services.Remove(scheduleLock);
        services.AddScoped<IServiceCodeLock>(provider => new RecordingCodeLock(
            (IServiceCodeLock)ActivatorUtilities.CreateInstance(provider, codeImplementation), this));
        services.AddScoped<IScheduleVersionsLock>(provider => new RecordingScheduleLock(
            (IScheduleVersionsLock)ActivatorUtilities.CreateInstance(provider, scheduleImplementation), this));
    }

    private sealed class RecordingCodeLock(IServiceCodeLock inner, RecordingLocks recorder) : IServiceCodeLock
    {
        public Task AcquireAsync(ServiceCode code, CancellationToken cancellationToken)
        {
            recorder._requests.Enqueue("timetable.ServiceCode:" + code.Value);
            return inner.AcquireAsync(code, cancellationToken);
        }
    }

    private sealed class RecordingScheduleLock(IScheduleVersionsLock inner, RecordingLocks recorder) : IScheduleVersionsLock
    {
        public Task AcquireAsync(CancellationToken cancellationToken)
        {
            recorder._requests.Enqueue(ScheduleVersions);
            return inner.AcquireAsync(cancellationToken);
        }
    }
}
