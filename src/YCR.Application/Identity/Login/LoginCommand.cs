namespace YCR.Application.Identity.Login;

/// <summary>Sign in with a username and password (spec §6.1 <c>POST /auth/login</c>).</summary>
public sealed record LoginCommand(string UserName, string Password)
{
    /// <summary>The password never reaches a log through this record (R13).</summary>
    public override string ToString() => "LoginCommand { UserName = ***, Password = *** }";
}
