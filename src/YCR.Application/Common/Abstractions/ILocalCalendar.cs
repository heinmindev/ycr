namespace YCR.Application.Common.Abstractions;

/// <summary>
/// Today's date in the configured local zone (ADR-0018 §Time; F-004 R38; plan P11, ruling Q3).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-25; plan Q3): the zone is <c>Time:LocalTimeZone</c>
/// (the IANA id <c>Asia/Yangon</c>), never hard-coded and never the server's local time. The
/// instant comes from <see cref="TimeProvider"/>, the only clock, so a test that owns the clock
/// owns "today". Handlers pass the date into the domain; the domain never reads a clock.
/// </remarks>
public interface ILocalCalendar
{
    DateOnly Today();
}
