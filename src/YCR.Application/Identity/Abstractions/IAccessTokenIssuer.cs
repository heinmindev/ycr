namespace YCR.Application.Identity.Abstractions;

/// <summary>
/// Issues the access JWT (ADR-0016; ADR-0023 items 7–8; spec R1): ES256 with a <c>kid</c>, 15
/// minutes, claims <c>sub</c>, <c>sid</c> and the registered claims only. Implemented in
/// <c>YCR.Api</c>, next to the handler that validates it.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId, Guid sessionId);
}

/// <param name="Value">The compact JWT. Returned to the caller only; never logged or audited (R13).</param>
/// <param name="ExpiresAtUtc">The token's <c>exp</c>.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc)
{
    public override string ToString() => $"AccessToken {{ Value = ***, ExpiresAtUtc = {ExpiresAtUtc:O} }}";
}
