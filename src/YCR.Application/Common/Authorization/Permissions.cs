namespace YCR.Application.Common.Authorization;

public static class Permissions
{
    /// <summary>
    /// ASSUMPTION (provisional, approved by hein 2026-09-20; replace when OQ26/OQ27/OQ28 answered):
    /// station-management permission constants are stable application policy names.
    /// </summary>
    public const string StationsManage = "stations.manage";

    public const string StationsRead = "stations.read";
}
