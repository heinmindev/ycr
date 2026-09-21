namespace YCR.TestSupport;

/// <summary>
/// The single pinned SQL Server 2022 image reference (spec E4, ADR-0017 item 5).
/// </summary>
/// <remarks>
/// ENGINEERING DECISION — spec E4: the image is pinned by tag <em>and</em> digest, and both
/// <c>docker-compose.yml</c> and the Testcontainers fixture must name exactly
/// <see cref="Reference"/>. A floating tag is never permitted: the ledger DDL, the edition
/// guard and the fragmentation baseline are all statements about one specific build of
/// SQL Server, and a tag that moves underneath them silently invalidates every one.
/// <para>
/// This constant is the authority. <c>docker-compose.yml</c> cannot import a C# constant, so
/// <c>SqlServerImagePinTests</c> reads the compose file and asserts it names this exact
/// reference — that test, not convention, is what keeps the two in step.
/// </para>
/// <para>
/// Pinned 2026-09-20: newest SQL Server 2022 cumulative update published on
/// <c>mcr.microsoft.com</c> at that date. To move to a newer CU, change both constants here,
/// re-run the container-backed suites and record the new tag, digest and date in
/// <c>docs/features/F-001-walking-skeleton/progress.md</c>.
/// </para>
/// </remarks>
public static class SqlServerImage
{
    /// <summary>Repository and tag, recorded so a human can tell which CU this is.</summary>
    public const string Tag = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";

    /// <summary>Manifest digest, which is what actually fixes the bits that run.</summary>
    public const string Digest = "sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";

    /// <summary>The reference to pass to Docker: tag for legibility, digest for identity.</summary>
    public const string Reference = Tag + "@" + Digest;

    /// <summary>
    /// The session language pinned on every connection string and login (hein's ruling,
    /// 2026-09-20).
    /// </summary>
    /// <remarks>
    /// SQL Server localises error messages by session language.
    /// <c>SqlServerUniqueConstraintTranslator</c> reads a violated constraint's name out of the
    /// message text, because SQL Server exposes it nowhere else, so the translator only works on
    /// an English session. Pinning the language turns that from an accident of the environment
    /// into a stated dependency, in compose, in the test fixture and in CI alike.
    /// </remarks>
    public const string SessionLanguage = "us_english";

    /// <summary>
    /// A SQL Server <strong>2019</strong> image, used by one trunk-only test to prove the audit
    /// ledger migration's version guard fires against a genuinely unsupported server (spec S19,
    /// ADR-0017 item 5).
    /// </summary>
    /// <remarks>
    /// Pinned by tag and digest for the same reason as <see cref="Reference"/>: the test asserts
    /// that the guard reports the detected version, so which build it runs against is part of
    /// what the test means. <c>SqlServerImagePinTests</c> checks this pin too.
    /// <para>
    /// Pinned 2026-09-21: newest SQL Server 2019 cumulative update published on
    /// <c>mcr.microsoft.com</c> at that date. 2019 is major version 15, below the 16 the guard
    /// requires — that is the whole point of keeping it.
    /// </para>
    /// <para>
    /// Trunk-only: this is a second large image pull, which spec E5 and the plan's review item 7
    /// judged too expensive for every branch build.
    /// </para>
    /// </remarks>
    public static class Unsupported
    {
        public const string Tag = "mcr.microsoft.com/mssql/server:2019-CU32-GDR1-ubuntu-20.04";

        public const string Digest = "sha256:cb917712eb2c8a1a497f71a0287ef9aaccd5ce549515c2ace0ae5e734f293d3e";

        public const string Reference = Tag + "@" + Digest;

        /// <summary>The major version this image reports, which the guard must reject.</summary>
        public const int ProductMajorVersion = 15;
    }
}
