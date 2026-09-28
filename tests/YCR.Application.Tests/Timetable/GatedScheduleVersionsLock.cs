using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Timetable.Abstractions;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// A decorator over the real <see cref="IScheduleVersionsLock"/> (the SQL Server applock), for the
/// forced-order race tests (F-005 plan §The Timetable-wide lock, "Forced orders"), the F-004
/// <see cref="GatedServiceCodeLock"/> technique. Operation 1 acquires the real lock and is held at
/// its gate; operation 2 is started and signals <see cref="ServiceCodeLockGate.Acquiring"/>; the
/// test asserts it has not <see cref="ServiceCodeLockGate.Acquired"/> after 500 ms — which only
/// the real SQL lock can cause — then releases operation 1.
/// </summary>
public sealed class GatedScheduleVersionsLock(IScheduleVersionsLock inner, ServiceCodeLockGate gate) : IScheduleVersionsLock
{
    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        gate.Acquiring.TrySetResult();
        await inner.AcquireAsync(cancellationToken);
        gate.Acquired.TrySetResult();
        await gate.Release.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Wraps whatever <c>AddInfrastructure</c> registered, so the real lock still runs.</summary>
    public static void Register(IServiceCollection services, ServiceCodeLockGate gate)
    {
        var real = services.Last(descriptor => descriptor.ServiceType == typeof(IScheduleVersionsLock));
        var implementation = real.ImplementationType
            ?? throw new InvalidOperationException("The real IScheduleVersionsLock is expected to be registered by type.");
        services.Remove(real);
        services.AddScoped<IScheduleVersionsLock>(provider => new GatedScheduleVersionsLock(
            (IScheduleVersionsLock)ActivatorUtilities.CreateInstance(provider, implementation),
            gate));
    }
}
