# ADR-0027: Timetable Times of Day

## Status

Proposed — 2026-09-26 (claude, T-053). Needs hein's approval to become Accepted. Parts of it are conditional on business answers (OQ52 precision, OQ53 past-midnight running) and say so.

## Context

**FACT — ADR-0018 §Time** fixes two representations: instants are `DateTimeOffset` / `datetimeoffset(3)` UTC, and calendar dates (`TravelDate`, `BusinessDate`, effective dates) are `DateOnly` / `date`. It has **no representation for a time of day**, and no timetable time exists in the code yet (F-004 stores no times, F-004 spec R22).

**FACT — the OQ47 ruling** (hein, 2026-09-25; provisional, not a Myanma Railways answer): a service's operating date is the Asia/Yangon calendar date on which it **starts**. F-004 said this "becomes meaningful only once FR-004 adds times".

**FACT — Asia/Yangon is UTC+06:30 with no daylight saving time** over 2026–2040, asserted by `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` (F-004 plan Amendment 1). A local wall-clock time on a given local date therefore maps to exactly one instant: no local time is skipped or repeated. The zone is read from `Time:LocalTimeZone` and never hard-coded (ADR-0018; `docs/20` §8).

**OPEN QUESTION — OQ53:** whether a service may run past midnight, how a time after midnight is written, and the maximum journey length. **OPEN QUESTION — OQ52:** whether times are whole minutes or seconds.

**Why a decision is needed now.** Times will be stored by the Timetable module and, if OQ4 binds tickets to a service departure, read by Ticketing and perhaps printed or encoded (OQ19). A representation chosen per feature would diverge; this one is cross-module, so it is recorded here rather than inside the F-005 spec.

## Options considered

1. **`TimeOnly` in code, SQL `time(0)`.** Natural type. Cannot hold a time after midnight on the operating date: `00:15` the next morning sorts *before* `23:50` the evening before, so "times increase along the stops" and "duration = arrival − departure" break for any service that crosses midnight. Needs a separate day-offset column to repair it (option 2).
2. **`TimeOnly` plus a day offset (`0` = operating date, `1` = the next day).** Correct, but two fields per time, every comparison has to combine them, and SQL checks must too.
3. **An offset from the operating date's local midnight, as one integer** (minutes, or seconds if OQ52 requires them). `0` = 00:00 on the operating date; `1440` = 00:00 the next day. Order and differences are plain integer arithmetic; a check constraint can bound it; the API renders it as `HH:mm` with hours allowed past 23 (`24:15` = 00:15 the next day), the convention public-transport timetable data such as GTFS uses for the same problem. Less self-describing in the database than `time`.
4. **Instants (`datetimeoffset`) per stop.** Ties a timetable, which repeats on every operating date, to one date. Wrong shape.

## Decision

**Proposed: option 3.**

1. A timetable time is a value object `TimetableTime` (name open at PLAN) in `YCR.Domain.Timetable`: a non-negative integer offset from **local midnight at the start of the service's operating date** (the OQ47 ruling), in the unit OQ52 decides — **whole minutes** unless OQ52 requires seconds. It is not an instant and has no zone of its own; the zone is the configured local zone.
2. SQL: `smallint` for minutes (or `int` for seconds), with a check constraint bounding it to `0 .. max`, where `max` follows OQ53: `1439` if no service may run past midnight; otherwise the maximum OQ53 sets (for example `2879`, under 48 hours, if a journey must end on the next day).
3. API: a string `HH:mm` (or `HH:mm:ss` if OQ52 requires seconds), two or more hour digits, hours `00`–`23` on the operating date and `24` and above after midnight **only if OQ53 allows past-midnight running**. Never a bare integer on the wire, and never a local date-time.
4. To turn a time into an instant (for example for a later validity check), the consumer computes local midnight of the operating date in the configured zone plus the offset, through one Timetable function over `ILocalCalendar`'s zone; no module adds `+06:30` itself.
5. Operating-date arithmetic stays `DateOnly` (ADR-0018). A time never changes which date a service operates on.

## Consequences

Positive:
- One comparable number per time; "times increase along the stops", dwell and run times are integer comparisons in the domain and in check constraints.
- Past-midnight running needs no second column and no special case, if OQ53 allows it.
- The API form is readable by staff and familiar from other timetable data.

Negative:
- The stored value is not human-readable in SQL without conversion; ad-hoc queries and reports must render it.
- `24:15` surprises readers who expect a 24-hour clock; any passenger-facing output (not in scope, F-005 spec §9) must render it as `00:15` with the next date.
- If OQ52 later changes minutes to seconds, the column type and API format change: a migration and an API version decision.

Follow-up work:
- If Accepted: add a "Times of day" line to `docs/20` §6 and a row to ADR-0018's representation list by a superseding or amending note (ADR-0018 is Accepted, so its Decision is not edited; this ADR is cited next to it instead).
- F-005 PLAN names the value object and its tests (bounds, rendering both ways, the midnight boundary under the configured zone).
