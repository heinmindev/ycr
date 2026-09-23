using System.Reflection;
using System.Text;

namespace YCR.Infrastructure.Identity;

/// <summary>
/// The offline common-password blocklist (ADR-0023 item 2, D2: no external breach service).
/// </summary>
/// <remarks>
/// Plan P9: an embedded resource derived from SecLists (MIT); source, commit, filter and the
/// file's SHA-256 are in <c>THIRD-PARTY-NOTICES.md</c>, and a test keeps that hash true. Entries
/// are lower-case and 12–128 characters long (shorter passwords are refused by length anyway), so
/// the comparison lower-cases the candidate with the same invariant rule. Loaded once per process.
/// </remarks>
internal sealed class CommonPasswordBlocklist
{
    public const string ResourceName = "YCR.Infrastructure.Identity.common-passwords.txt";

    private readonly HashSet<string> entries;

    public CommonPasswordBlocklist()
    {
        entries = new HashSet<string>(ReadEntries(), StringComparer.Ordinal);
    }

    public int Count => entries.Count;

    public bool Contains(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return entries.Contains(password.ToLowerInvariant());
    }

    /// <summary>The embedded file's exact bytes, for the provenance test.</summary>
    public static byte[] ReadResourceBytes()
    {
        using var stream = OpenResource();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static IEnumerable<string> ReadEntries()
    {
        using var reader = new StreamReader(OpenResource(), new UTF8Encoding(false));
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    private static Stream OpenResource() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"The embedded blocklist '{ResourceName}' is missing.");
}
