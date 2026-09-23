using YCR.Application.Common.Abstractions;

namespace YCR.Application.Identity.Violations;

/// <summary>
/// F-002 plan P5: names an actor through <c>RecordSignIn</c> from somewhere other than
/// <c>LoginHandler</c> — the one caller allowed to, because only it has just verified the password.
/// </summary>
public sealed class RecordSignInOutsideLoginHandler(IAuditWriter audit)
{
    public void ForgeASignIn(Guid someoneElse) =>
        audit.RecordSignIn(someoneElse, ["SystemAdministrator"], "Identity.LoginSucceeded", "Identity.User", after: null);
}
