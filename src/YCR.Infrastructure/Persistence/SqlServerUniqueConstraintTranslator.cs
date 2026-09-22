using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using YCR.Application.Common;

namespace YCR.Infrastructure.Persistence;

/// <summary>
/// Translates SQL Server unique-constraint errors into the provider-neutral
/// <see cref="UniqueConstraintViolationException"/> (tech-lead ruling, plan §Unique-constraint
/// translation).
/// </summary>
/// <remarks>
/// This is the only place that knows SQL Server error numbers <strong>2601</strong> (duplicate
/// key in a unique index) and <strong>2627</strong> (unique key constraint violation). Keeping
/// it in Infrastructure is what lets Application catch a uniqueness failure without referencing
/// the provider, which ADR-0004 requires and an architecture test enforces.
/// <para>
/// SQL Server exposes the offending constraint name only inside the message text, so it has to
/// be parsed. That is a real weakness and it is handled rather than hidden: when the name cannot
/// be extracted — a localised server, or a message format that changes — this returns
/// <see langword="null"/> and the original exception propagates untranslated. A write then fails
/// as an unexpected infrastructure error, which is correct, instead of being reported as whatever
/// conflict the nearest handler happens to know about.
/// </para>
/// <para>
/// <strong>REQUIRED CONTROL — session language.</strong> Because the name is parsed from message
/// text, this class only works on a <c>us_english</c> session: SQL Server localises error
/// messages by session language, and on, say, a German session error 2601 reads "Doppelter
/// Schlüssel..." and matches nothing here. Every connection therefore pins
/// <c>Current Language=us_english</c> — in the test fixture
/// (<c>YCR.TestSupport.SqlServerImage.SessionLanguage</c>), in the connection strings documented
/// in <c>.env.example</c>, and in CI — and both logins created by
/// <c>docker/sqlserver/init-principals.sql</c> additionally set <c>DEFAULT_LANGUAGE = us_english</c>
/// so a tool that connects without naming a language still lands in English.
/// <c>UniqueConstraintTranslationTests</c> asserts the session language the translator's own
/// inputs came from, so the dependency cannot rot unnoticed (hein's ruling, 2026-09-20).
/// </para>
/// </remarks>
internal static partial class SqlServerUniqueConstraintTranslator
{
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int UniqueKeyConstraintViolation = 2627;

    /// <summary>
    /// Returns the translated exception, or <see langword="null"/> when
    /// <paramref name="exception"/> is not a recognisable unique-constraint violation.
    /// </summary>
    public static UniqueConstraintViolationException? Translate(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sqlException)
        {
            return null;
        }

        var constraintName = sqlException.Number switch
        {
            DuplicateKeyInUniqueIndex => UniqueIndexName().Match(sqlException.Message),
            UniqueKeyConstraintViolation => UniqueConstraintName().Match(sqlException.Message),
            _ => null
        };

        return constraintName is { Success: true }
            ? new UniqueConstraintViolationException(constraintName.Groups["name"].Value, exception)
            : null;
    }

    /// <summary>Error 2601: "... with unique index 'UX_Stations_Code'."</summary>
    [GeneratedRegex(@"with unique index '(?<name>[^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex UniqueIndexName();

    /// <summary>Error 2627: "Violation of UNIQUE KEY constraint 'UQ_...'."</summary>
    [GeneratedRegex(@"UNIQUE KEY constraint '(?<name>[^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex UniqueConstraintName();
}
