namespace YCR.Application.Network;

/// <summary>
/// Database constraint names the Network module's handlers recognise.
/// </summary>
/// <remarks>
/// A handler catches <see cref="Common.UniqueConstraintViolationException"/> and must match on
/// the constraint name, so the name has to be stated somewhere Application can see. The index
/// itself is declared by <c>StationConfiguration</c> in Infrastructure, which Application may not
/// reference.
/// <para>
/// The two are kept honest by <c>StationModelTests</c>, which asserts the mapped index name
/// equals this constant. Without that assertion a rename in Infrastructure would turn a
/// <c>409 Conflict</c> into an unhandled <c>500</c>, and nothing would fail until a duplicate
/// code was posted in production.
/// </para>
/// </remarks>
public static class NetworkConstraints
{
    public const string StationCodeUniqueIndex = "UX_Stations_Code";
}
