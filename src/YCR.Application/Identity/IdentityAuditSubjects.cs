namespace YCR.Application.Identity;

/// <summary>
/// The audit subject types the Identity module writes (ADR-0021 <c>SubjectType</c>; the
/// <c>NetworkAuditSubjects</c> pattern). Changing a value rewrites the meaning of history.
/// </summary>
/// <remarks>
/// G1 (hein, 2026-09-23; T-024): every Identity event, including <c>Identity.SessionRevoked</c>
/// and <c>Identity.RefreshFamilyRevoked</c>, has the <b>user</b> as its subject, so a user's whole
/// history — revocations included — is found under one subject. The session id and the
/// revocation reason are in <c>AfterJson</c> (<see cref="AuthSessionAuditSnapshot"/>).
/// </remarks>
public static class IdentityAuditSubjects
{
    public const string User = "Identity.User";
}
