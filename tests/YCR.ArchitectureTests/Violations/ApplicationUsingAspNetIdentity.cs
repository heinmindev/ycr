using Microsoft.AspNetCore.Identity;

namespace YCR.Application.Violations;

/// <summary>F-002 plan P1: ASP.NET Core Identity stays in Infrastructure, behind IPasswordService.</summary>
public sealed class ApplicationUsingAspNetIdentity
{
    public PasswordHasher<object> Hasher { get; } = new();
}
