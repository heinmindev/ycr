namespace YCR.Application.Identity.ChangeOwnPassword;

/// <summary>
/// Change one's own password (spec §6.1 <c>POST /auth/password</c>). <paramref name="SessionId"/>
/// is the calling session's <c>sid</c>, which survives (R4).
/// </summary>
public sealed record ChangeOwnPasswordCommand(Guid SessionId, string CurrentPassword, string NewPassword)
{
    public override string ToString() => $"ChangeOwnPasswordCommand {{ SessionId = {SessionId}, CurrentPassword = ***, NewPassword = *** }}";
}
