using System.Text.RegularExpressions;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Ci;

/// <summary>
/// Every CI job that builds the migration bundle must define
/// <see cref="YcrDbContextFactory.DesignTimeConnectionVariable"/>, because the design-time factory
/// has no default and `dotnet ef` fails without it.
/// </summary>
/// <remarks>
/// The regression this covers was real: a push failed because the variable was declared only at
/// step scope and the bundle process did not inherit it. There is no way to unit-test workflow
/// wiring other than by reading the workflow.
/// <para>
/// R-3 cut this down to exactly that one claim. The previous version asserted an exact multi-line
/// substring containing each job's <c>timeout-minutes</c> value and required <c>env</c> to be the
/// very next key — so changing a timeout, reordering two keys or adding a comment between them
/// broke a test about design-time connections. Nothing here depends on indentation, key order or
/// any value other than the variable's name.
/// </para>
/// </remarks>
public sealed class CiWorkflowTests
{
    [Fact]
    public void EveryBundleBuildingJob_DefinesTheDesignTimeConnection()
    {
        var jobs = JobBlocks(ReadWorkflow())
            .Where(job => job.Body.Contains("dotnet ef migrations bundle", StringComparison.Ordinal))
            .ToArray();

        // Guard against the rule passing because the search found nothing.
        Assert.NotEmpty(jobs);

        Assert.All(jobs, job => Assert.Contains(
            YcrDbContextFactory.DesignTimeConnectionVariable,
            job.Body,
            StringComparison.Ordinal));
    }

    private static IEnumerable<(string Name, string Body)> JobBlocks(string workflow)
    {
        // A job header is the only two-space-indented key under `jobs:`; everything inside a job
        // is indented further. Splitting on that is enough to attribute a step to its job.
        var headers = Regex.Matches(workflow, @"^  (?<name>[A-Za-z0-9_-]+):$", RegexOptions.Multiline)
            .ToArray();

        for (var i = 0; i < headers.Length; i++)
        {
            var start = headers[i].Index;
            var end = i + 1 < headers.Length ? headers[i + 1].Index : workflow.Length;

            yield return (headers[i].Groups["name"].Value, workflow[start..end]);
        }
    }

    private static string ReadWorkflow()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".github", "workflows", "ci.yml");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Replace("\r\n", "\n", StringComparison.Ordinal);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The CI workflow was not found above the test output directory.");
    }
}
