# ADR-0027: Timetable Times of Day

## Status

Accepted — 2026-09-26 (hein, T-053), with a maximum of **1439 minutes** under the OQ53 ruling (no running past midnight in Phase 1). Drafted by claude (T-053) and proposed the same day. The conditional parts below are settled by the OQ52 ruling (whole minutes) and the OQ53 ruling (no past-midnight running); each is marked where it applies.

## Context

**FACT — ADR-0018 §Time** fixes two representations: instants are `DateTimeOffset` / `datetimeoffset(3)` UTC, and calendar dates (`TravelDate`, `BusinessDate`, effective dates) are `DateOnly` / `date`. It has **no representation for a time of day**, and no timetable time exists in the code yet (F-004 stores no times, F-004 spec R22).

**FACT — the OQ47 ruling** (hein, 2026-09-25; provisional, not a Myanma Railways answer): a service's operating date is the Asia/Yangon calendar date on which it **starts**. F-004 said this "becomes meaningful only once FR-004 adds times".

**FACT — Asia/Yangon is UTC+06:30 with no daylight saving time** over 2026–2040, asserted by `LocalCalendarTests.AsiaYangon_ResolvesInThisEnvironment` (F-004 plan Amendment 1). A local wall-clock time on a given local date therefore maps to exactly one instant: no local time is skipped or repeated. The zone is read from `Time:LocalTimeZone` and never hard-coded (ADR-0018; `docs/20` §8).

**Asked at discovery as OQ53** (whether a service may run past midnight, how a later time is written, the maximum journey length) **and OQ52** (whole minutes or seconds). **Resolved by tech-lead rulings (hein, 2026-09-26; T-053) — not Myanma Railways answers:** whole minutes; no running past midnight in Phase 1 (every time 00:00–23:59 on the operating date). Still open with Myanma Railways; an official, different answer supersedes the rulings and needs its own follow-up task.

**Why a decision is needed now.** Times will be stored by the Timetable module and, if OQ4 binds tickets to a service departure, read by Ticketing and perhaps printed or encoded (OQ19). A representation chosen per feature would diverge; this one is cross-module, so it is recorded here rather than inside the F-005 spec.

## Options considered

1. **`TimeOnly` in code, SQL `time(0)`.** Natural type. Cannot hold a time after midnight on the operating date: `00:15` the next morning sorts *before* `23:50` the evening before, so "times increase along the stops" and "duration = arrival − departure" break for any service that crosses midnight. Needs a separate day-offset column to repair it (option 2).
2. **`TimeOnly` plus a day offset (`0` = operating date, `1` = the next day).** Correct, but two fields per time, every comparison has to combine them, and SQL checks must too.
3. **An offset from the operating date's local midnight, as one integer** (minutes, or seconds if OQ52 requires them). `0` = 00:00 on the operating date; `1440` = 00:00 the next day. Order and differences are plain integer arithmetic; a check constraint can bound it; the API renders it as `HH:mm` with hours allowed past 23 (`24:15` = 00:15 the next day), the convention public-transport timetable data such as GTFS uses for the same problem. Less self-describing in the database than `time`.
4. **Instants (`datetimeoffset`) per stop.** Ties a timetable, which repeats on every operating date, to one date. Wrong shape.

## Decision

**Option 3** (accepted by hein, 2026-09-26).

1. A timetable time is a value object `TimetableTime` (name open at PLAN) in `YCR.Domain.Timetable`: a non-negative integer offset from **local midnight at the start of the service's operating date** (the OQ47 ruling), in **whole minutes** (OQ52 ruling, hein, 2026-09-26). It is not an instant and has no zone of its own; the zone is the configured local zone.
2. SQL: `smallint`, with a check constraint bounding it to `0 .. 1439` (00:00–23:59): the OQ53 ruling (hein, 2026-09-26) allows no running past midnight in Phase 1, so every time falls on the operating date. If a later ruling allows past-midnight running, the bound is widened by a new migration and this item is superseded (the representation itself already allows it).
3. API: a string `HH:mm`, exactly two hour digits `00`–`23` and two minute digits `00`–`59`. Hours `24` and above are refused while the OQ53 ruling stands. Never a bare integer on the wire, and never a local date-time.
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
- **Done at F-005 (recorded 2026-09-28, T-059).** The value object is named `TimetableTime` (F-005 plan P2; `src/YCR.Domain/Timetable/TimetableTime.cs`): `short Minutes` in `0..1439`, parsed from exactly `HH:mm` with `[0-9]` (not `\d`, which also matches Myanmar digits). Item 2's check is `CK_ScheduleStopTimes_Minutes` (`docs/07` §F-005). Item 4's time-to-instant function is **not built**: nothing in F-005 consumes an instant; the first consumer builds it. The `docs/20` §6 "Times of day" line is added; ADR-0018 is not edited (it is Accepted), this ADR is cited next to it.
