using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace YCR.Application.Timetable;

/// <summary>
/// The <c>stopTimesSha256</c> of a version's creation audit event (F-005 spec §8, E13; plan P18).
/// </summary>
/// <remarks>
/// <para>
/// ENGINEERING DECISION (tech lead, hein, 2026-09-26; E13 accepted; canonical form fixed by the
/// plan). The ledger row carries a digest, not every time: the times live in insert-only rows that
/// never change after creation (R25, R32), and anyone can recompute this from them (R36).
/// </para>
/// <para>
/// <strong>Canonical form — changing it bumps <c>PayloadVersion</c></strong> (ADR-0021 rule 3):
/// one line per stop time, <c>"{serviceId}|{position}|{arrivalMinute}|{departureMinute}\n"</c>,
/// where <c>serviceId</c> is the lower-case <c>D</c> format, the numbers are invariant-culture
/// decimal integers, and a missing time is the empty string; lines ordered by <c>serviceId</c>
/// (ordinal) then <c>position</c> (ascending); UTF-8 without a BOM; SHA-256; lower-case hex, 64
/// characters. A version with no stop times hashes the empty input,
/// <c>e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855</c>.
/// </para>
/// </remarks>
public static class ScheduleStopTimesDigest
{
    public static string Compute(IEnumerable<ScheduleStopTimeDigestRow> stopTimes)
    {
        ArgumentNullException.ThrowIfNull(stopTimes);

        var text = new StringBuilder();
        foreach (var row in stopTimes
            .Select(row => (ServiceId: row.ServiceId.ToString("D"), row.Position, row.ArrivalMinute, row.DepartureMinute))
            .OrderBy(row => row.ServiceId, StringComparer.Ordinal)
            .ThenBy(row => row.Position))
        {
            text.Append(row.ServiceId)
                .Append('|')
                .Append(row.Position.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(row.ArrivalMinute?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
                .Append('|')
                .Append(row.DepartureMinute?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
                .Append('\n');
        }

        var hash = SHA256.HashData(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}

/// <summary>One stop time as the digest reads it; a <c>null</c> minute is a missing time.</summary>
public sealed record ScheduleStopTimeDigestRow(Guid ServiceId, int Position, short? ArrivalMinute, short? DepartureMinute);
