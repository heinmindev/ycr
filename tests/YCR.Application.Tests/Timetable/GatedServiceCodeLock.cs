using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Timetable.Abstractions;
using YCR.Domain.Timetable;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// One operation's view of the code lock, for the forced-order race tests (F-004 plan §R35 "How it
/// is tested"): it says when the operation starts <see cref="Acquiring"/> the real lock, when it has
/// <see cref="Acquired"/> it, and then holds the operation until <see cref="Release"/> opens.
/// </summary>
public sealed class ServiceCodeLockGate
{
    public TaskCompletionSource Acquiring { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A gate that never holds the operation: it only reports.</summary>
    public static ServiceCodeLockGate Open()
    {
        var gate = new ServiceCodeLockGate();
        gate.Release.SetResult();
        return gate;
    }
}

/// <summary>
/// A decorator over the real <see cref="IServiceCodeLock"/> (the SQL Server applock). The order of
/// two operations is fixed by construction: operation 1 acquires the real lock and is held at its
/// gate; operation 2 is started and signals <see cref="ServiceCodeLockGate.Acquiring"/>; the test
/// asserts it has not <see cref="ServiceCodeLockGate.Acquired"/> after 500 ms — which only the real
/// SQL lock can cause — then releases operation 1.
/// </summary>
public sealed class GatedServiceCodeLock(IServiceCodeLock inner, ServiceCodeLockGate gate) : IServiceCodeLock
{
    public async Task AcquireAsync(ServiceCode code, CancellationToken cancellationToken)
    {
        gate.Acquiring.TrySetResult();
        await inner.AcquireAsync(code, cancellationToken);
        gate.Acquired.TrySetResult();
        await gate.Release.Task.WaitAsync(cancellationToken);
    }

    /// <summary>Wraps whatever <c>AddInfrastructure</c> registered, so the real lock still runs.</summary>
    public static void Register(IServiceCollection services, ServiceCodeLockGate gate)
    {
        var real = services.Last(descriptor => descriptor.ServiceType == typeof(IServiceCodeLock));
        var implementation = real.ImplementationType
            ?? throw new InvalidOperationException("The real IServiceCodeLock is expected to be registered by type.");
        services.Remove(real);
        services.AddScoped<IServiceCodeLock>(provider => new GatedServiceCodeLock(
            (IServiceCodeLock)ActivatorUtilities.CreateInstance(provider, implementation),
            gate));
    }
}
