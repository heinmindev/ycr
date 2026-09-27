using Microsoft.Extensions.DependencyInjection;
using YCR.Application.Common.Authorization;
using YCR.Application.Timetable.CancelScheduleVersion;
using YCR.Application.Timetable.CreateScheduleVersion;
using YCR.Application.Timetable.DiscardScheduleVersion;
using YCR.Application.Timetable.PublishScheduleVersion;
using YCR.Application.Tests.Network;
using YCR.Domain.Common;
using YCR.TestSupport;
using static YCR.Application.Tests.Timetable.TimetableTestData;

namespace YCR.Application.Tests.Timetable;

/// <summary>
/// F-005 spec §4's setting: route RC closed <c>[A, B, C, D, E]</c>; S1 <c>[A, C, E]</c> Forward,
/// Monday–Friday, from 2026-10-05, open-ended; S2 <c>[E, C, A]</c> Reverse, every day, from
/// 2026-10-05, open-ended. Both are created through the real F-004 handler.
/// </summary>
public sealed record ScheduleNetwork(TimetableNetwork Network, Guid S1, Guid S2);

/// <summary>Spec §4's default create ("a valid create"), with named overrides.</summary>
public static class ScheduleTestData
{
    public static readonly DayOfWeek[] EveryDay = Enum.GetValues<DayOfWeek>();

    /// <summary>S1 with <c>A dep 06:00</c>, <c>C arr 06:20 dep 06:22</c>, <c>E arr 06:40</c> (spec §4).</summary>
    public static CreateScheduleServiceItem ValidS1(Guid s1) => Times(s1, (null, "06:00"), ("06:20", "06:22"), ("06:40", null));

    /// <summary>A three-stop service's times (S2 or any <c>[x, y, z]</c> service): 07:00, 07:20/07:21, 07:40.</summary>
    public static CreateScheduleServiceItem ValidThreeStop(Guid serviceId) =>
        Times(serviceId, (null, "07:00"), ("07:20", "07:21"), ("07:40", null));

    /// <summary>Stop times in position order 1..k.</summary>
    public static CreateScheduleServiceItem Times(Guid serviceId, params (string? Arrival, string? Departure)[] stops) =>
        new(serviceId, [.. stops.Select((stop, index) => new CreateScheduleStopTimeItem(index + 1, stop.Arrival, stop.Departure))]);

    public static CreateScheduleVersionCommand Version(
        string effectiveFrom,
        IReadOnlyList<CreateScheduleServiceItem> services,
        string? nameEn = "October 2026",
        string? nameMy = "အောက်တိုဘာ") =>
        new(nameEn, nameMy, Date(effectiveFrom), services);
}

/// <summary>
/// Shared setup for the schedule handler suites (F-005 plan §Test plan): everything
/// <see cref="TimetableHandlerTestBase"/> provides — real SQL Server under <c>ycr_app</c>, the test
/// clock at 2026-10-01 03:00 UTC (today 2026-10-01 in Asia/Yangon) — plus a schedule manager and
/// the four write handlers.
/// </summary>
public abstract class ScheduleHandlerTestBase(SqlServerFixture fixture) : TimetableHandlerTestBase(fixture)
{
    protected static readonly TimeSpan LockWait = TimeSpan.FromSeconds(30);

    /// <summary>A schedule administrator, as the API would present one.</summary>
    protected static TestCurrentUser ScheduleManager { get; } = new()
    {
        UserId = Guid.Parse("0199b3a0-0000-7000-8000-00000000cccc"),
        Roles = ["RailwayAdministrator"],
        ClientIp = "198.51.100.9",
        CorrelationId = "corr-schedule",
        AuthorizedByPermission = Permissions.SchedulesManage
    };

    protected ServiceProvider BuildScheduleProvider(TestClock? clock = null, Action<IServiceCollection>? configure = null) =>
        BuildServiceProvider(ScheduleManager, clock, configure);

    /// <summary>Spec §4's network and services S1 and S2.</summary>
    protected static async Task<ScheduleNetwork> CreateScheduleNetworkAsync(IServiceProvider provider)
    {
        var network = await CreateNetworkAsync(provider);
        var s1 = await CreateServiceOrFailAsync(provider, Command(network));
        var s2 = await CreateServiceOrFailAsync(provider, Command(network, stops: "ECA", code: "S202", direction: "Reverse", days: ScheduleTestData.EveryDay));
        return new ScheduleNetwork(network, s1, s2);
    }

    protected static async Task<Result<CreatedScheduleVersion>> CreateVersionAsync(IServiceProvider provider, CreateScheduleVersionCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateScheduleVersionHandler>().Handle(command, CancellationToken);
    }

    protected static async Task<CreatedScheduleVersion> CreateVersionOrFailAsync(IServiceProvider provider, CreateScheduleVersionCommand command)
    {
        var result = await CreateVersionAsync(provider, command);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    protected static async Task<Result> PublishAsync(IServiceProvider provider, Guid versionId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PublishScheduleVersionHandler>()
            .Handle(new PublishScheduleVersionCommand(versionId), CancellationToken);
    }

    protected static async Task<Result> DiscardAsync(IServiceProvider provider, Guid versionId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DiscardScheduleVersionHandler>()
            .Handle(new DiscardScheduleVersionCommand(versionId), CancellationToken);
    }

    protected static async Task<Result> CancelAsync(IServiceProvider provider, Guid versionId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CancelScheduleVersionHandler>()
            .Handle(new CancelScheduleVersionCommand(versionId), CancellationToken);
    }

    /// <summary>Creates and publishes a version; returns its id.</summary>
    protected static async Task<Guid> PublishedVersionAsync(
        IServiceProvider provider, string effectiveFrom, IReadOnlyList<CreateScheduleServiceItem> services)
    {
        var created = await CreateVersionOrFailAsync(provider, ScheduleTestData.Version(effectiveFrom, services));
        var published = await PublishAsync(provider, created.Id);
        Assert.True(published.IsSuccess, published.IsFailure ? published.Error.Code : null);
        return created.Id;
    }

    /// <summary>"Nothing written" for versions (spec §4): no version, entry or time row, and no schedule event.</summary>
    protected async Task AssertNoScheduleRowsAsync(int versions = 0, int entries = 0, int stopTimes = 0, int events = 0)
    {
        Assert.Equal(versions, await CountAsync("[timetable].[ScheduleVersions]"));
        Assert.Equal(entries, await CountAsync("[timetable].[ScheduleVersionServices]"));
        Assert.Equal(stopTimes, await CountAsync("[timetable].[ScheduleStopTimes]"));
        Assert.Equal(events, await CountAsync("[audit].[AuditEvents] WHERE [Action] LIKE N'Timetable.ScheduleVersion%'"));
    }

    protected async Task<string> StatusOfAsync(Guid versionId) =>
        (await ScalarAsync<string>($"SELECT [Status] FROM [timetable].[ScheduleVersions] WHERE [Id] = '{versionId}';"))!;

    /// <summary>
    /// Runs two operations in a fixed order on the real Timetable-wide lock (plan §The Timetable-wide
    /// lock, "Forced orders"): the first acquires it and is held at its gate; the second starts, is
    /// seen to be waiting for the lock after 500 ms, and only then is the first released.
    /// </summary>
    protected async Task<(T1 First, T2 Second)> ForcedOnScheduleLockAsync<T1, T2>(
        Func<IServiceProvider, Task<T1>> first,
        Func<IServiceProvider, Task<T2>> second)
    {
        var firstGate = new ServiceCodeLockGate();
        var secondGate = ServiceCodeLockGate.Open();
        await using var firstProvider = BuildScheduleProvider(configure: services => GatedScheduleVersionsLock.Register(services, firstGate));
        await using var secondProvider = BuildScheduleProvider(configure: services => GatedScheduleVersionsLock.Register(services, secondGate));

        var firstRun = first(firstProvider);
        Task<T2> secondRun;
        try
        {
            await firstGate.Acquired.Task.WaitAsync(LockWait, CancellationToken);
            secondRun = second(secondProvider);
            await secondGate.Acquiring.Task.WaitAsync(LockWait, CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken);
            Assert.False(
                secondGate.Acquired.Task.IsCompleted,
                "The second operation acquired the schedule lock while the first still held it.");
        }
        finally
        {
            firstGate.Release.TrySetResult();
        }

        return (await firstRun, await secondRun);
    }
}
