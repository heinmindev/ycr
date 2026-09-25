namespace YCR.Application.Timetable.WithdrawService;

/// <summary>Withdraws a service from a date (F-004 S30; R21).</summary>
/// <remarks>Carries no actor, address or correlation field (ADR-0017 item 2; S45).</remarks>
/// <param name="WithdrawFrom">The first date on which the service no longer runs.</param>
public sealed record WithdrawServiceCommand(Guid ServiceId, DateOnly WithdrawFrom);
