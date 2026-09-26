using Microsoft.Extensions.Options;
using YCR.Application.Common.Abstractions;

namespace YCR.Infrastructure.Time;

/// <summary>
/// Today's date in the configured local zone: <see cref="TimeProvider"/>'s UTC instant converted to
/// <c>Time:LocalTimeZone</c> (ADR-0018 §Time; F-004 R38; plan P11).
/// </summary>
/// <remarks>
/// Asia/Yangon is UTC+06:30 with no daylight saving, so the local date changes at 17:30:00Z. The
/// zone is resolved once, on first use, by <see cref="LocalTimeOptions.ResolveZone"/>; the hosts
/// have already resolved it at startup, so a missing zone never reaches a request.
/// </remarks>
internal sealed class LocalCalendar(TimeProvider clock, IOptions<LocalTimeOptions> options) : ILocalCalendar
{
    private readonly Lazy<TimeZoneInfo> zone = new(() => LocalTimeOptions.ResolveZone(options.Value.LocalTimeZone));

    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone.Value).DateTime);
}
