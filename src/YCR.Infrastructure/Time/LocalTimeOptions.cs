namespace YCR.Infrastructure.Time;

/// <summary>
/// The configured local zone, <c>Time:LocalTimeZone</c> (ADR-0018 §Time; F-004 plan P11, ruling Q3).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-25; plan Q3): the value is the IANA id
/// <c>Asia/Yangon</c>, shipped in the <c>appsettings.json</c> of both the Api and the Worker. Both
/// hosts resolve it at startup through <see cref="ResolveZone"/> and refuse to start if it is missing
/// or this host cannot resolve it — fail closed, never a fixed-offset fallback. Resolving needs ICU on
/// Windows and IANA time-zone data (<c>tzdata</c>) on Linux (plan R-4).
/// </remarks>
public sealed class LocalTimeOptions
{
    /// <summary>The configuration section: <c>Time</c>.</summary>
    public const string SectionName = "Time";

    /// <summary>The full key, as error messages name it.</summary>
    public const string ConfigurationKey = "Time:LocalTimeZone";

    /// <summary>An IANA time-zone id, e.g. <c>Asia/Yangon</c>.</summary>
    public string? LocalTimeZone { get; set; }

    /// <summary>The one zone resolver both hosts use.</summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="id"/> is missing, blank or not a zone this host can resolve. The message names
    /// <c>Time:LocalTimeZone</c> and the id.
    /// </exception>
    public static TimeZoneInfo ResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is not configured. Set it to an IANA time-zone id such as 'Asia/Yangon'.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} '{id}' is not a time zone this host can resolve; install IANA time-zone data.",
                exception);
        }
    }
}
