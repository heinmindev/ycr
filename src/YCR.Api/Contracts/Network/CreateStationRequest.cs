using FluentValidation;
using YCR.Application.Network.CreateStation;

namespace YCR.Api.Contracts.Network;

/// <summary>The body of <c>POST /api/v1/stations</c> (docs/20 §2).</summary>
/// <remarks>
/// Carries no actor, address or correlation field, and must not gain one: ADR-0017 item 2
/// requires those to come from the authenticated server context, and the surest way to keep a
/// request from influencing them is to give it nowhere to put them (spec S20).
/// </remarks>
public sealed record CreateStationRequest(string? Code, string? NameEn, string? NameMy)
{
    public CreateStationCommand ToCommand() =>
        new(Code ?? string.Empty, NameEn ?? string.Empty, NameMy ?? string.Empty);
}

/// <summary>
/// Shape-level validation only (docs/20 §3).
/// </summary>
/// <remarks>
/// This checks that the fields are present. Whether a code or a name is <em>valid</em> is a
/// business rule and stays in <c>StationCode</c> and <c>BilingualName</c>, which are the sole
/// homes of the provisional rules R3 and R4 under the approved waiver. Duplicating the rules
/// here would create a second place to change when OQ26 and OQ27 are answered, and the two
/// would drift.
/// <para>
/// Both paths produce a 400, so a caller cannot tell which layer rejected the request.
/// </para>
/// </remarks>
public sealed class CreateStationRequestValidator : AbstractValidator<CreateStationRequest>
{
    public CreateStationRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty();
        RuleFor(request => request.NameEn).NotEmpty();
        RuleFor(request => request.NameMy).NotEmpty();
    }
}
