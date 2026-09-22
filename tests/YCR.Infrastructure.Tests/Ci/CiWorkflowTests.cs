using System.Text.RegularExpressions;
using YCR.Infrastructure.Persistence;

namespace YCR.Infrastructure.Tests.Ci;

/// <summary>
/// Every CI job that builds the migration bundle must define
/// <see cref="YcrDbContextFactory.DesignTimeConnectionVariable"/> before it runs
/// <c>dotnet ef migrations bundle</c>, because the design-time factory has no default and
/// `dotnet ef` fails without it.
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
/// <para>
/// N-1 (T-006a re-review at 52326b3): the earlier version searched the whole job body, so it
/// could not tell a job-level <c>env:</c> declaration from an unrelated later occurrence of the
/// same variable name. <c>api-smoke</c>'s "Apply migrations and finish provisioning" step
/// contains a step-scoped shell override of the same variable (S-007A-2) *after* it runs
/// <c>dotnet ef migrations bundle</c>; that override kept a whole-job substring search passing
/// even with the job-level declaration deleted. The search is now scoped to the text that
/// precedes the bundle-build command — where the override cannot reach — and the two facts below
/// pin that behaviour with the exact mutation the re-review used.
/// </para>
/// </remarks>
public sealed class CiWorkflowTests
{
    private const string BundleBuildCommand = "dotnet ef migrations bundle";

    [Fact]
    public void EveryBundleBuildingJob_DefinesTheDesignTimeConnectionBeforeBuildingTheBundle()
    {
        var jobs = JobBlocks(ReadWorkflow())
            .Where(job => job.Body.Contains(BundleBuildCommand, StringComparison.Ordinal))
            .ToArray();

        // Guard against the rule passing because the search found nothing.
        Assert.NotEmpty(jobs);

        Assert.All(jobs, job => Assert.True(
            DefinesConnectionBeforeBundleBuild(job.Body),
            $"Job '{job.Name}' does not define {YcrDbContextFactory.DesignTimeConnectionVariable} " +
            $"before it runs '{BundleBuildCommand}'."));
    }

    /// <summary>
    /// N-1 mutation 1: deleting the job-level <c>env:</c> line for <c>api-smoke</c> must make the
    /// check fail — that line is the only thing in scope before the bundle-build command runs.
    /// </summary>
    [Fact]
    public void DefinesConnectionBeforeBundleBuild_WithTheJobLevelEnvLineDeleted_IsFalse()
    {
        var job = ApiSmokeJobBody();
        var mutated = RemoveLineContaining(job, $"{YcrDbContextFactory.DesignTimeConnectionVariable}:");

        Assert.False(DefinesConnectionBeforeBundleBuild(mutated));
    }

    /// <summary>
    /// N-1 mutation 2: deleting only the step-level shell override (the S-007A-2 apply-time
    /// connection, which runs after the bundle is built) must leave the check passing — it never
    /// contributed to satisfying it.
    /// </summary>
    [Fact]
    public void DefinesConnectionBeforeBundleBuild_WithOnlyTheStepLevelOverrideDeleted_IsTrue()
    {
        var job = ApiSmokeJobBody();
        var mutated = RemoveLineContaining(
            job, $"{YcrDbContextFactory.DesignTimeConnectionVariable}=\"Server=localhost,1433");

        Assert.True(DefinesConnectionBeforeBundleBuild(mutated));
    }

    /// <summary>
    /// True when <paramref name="jobBody"/> defines the design-time connection variable somewhere
    /// before the first place it runs <c>dotnet ef migrations bundle</c>. Text after that point —
    /// such as a step that later overrides the variable to apply the bundle to a real database —
    /// cannot satisfy the check, because it is not in scope for the process that builds the
    /// bundle.
    /// </summary>
    private static bool DefinesConnectionBeforeBundleBuild(string jobBody)
    {
        var buildIndex = jobBody.IndexOf(BundleBuildCommand, StringComparison.Ordinal);
        if (buildIndex < 0)
        {
            throw new InvalidOperationException(
                $"Job body does not contain '{BundleBuildCommand}'; this helper is only valid for a " +
                "bundle-building job.");
        }

        return jobBody[..buildIndex].Contains(
            YcrDbContextFactory.DesignTimeConnectionVariable, StringComparison.Ordinal);
    }

    private static string ApiSmokeJobBody()
    {
        var job = JobBlocks(ReadWorkflow()).Single(j => j.Name == "api-smoke");
        return job.Body;
    }

    private static string RemoveLineContaining(string text, string needle)
    {
        var lines = text.Split('\n');
        var kept = lines.Where(line => !line.Contains(needle, StringComparison.Ordinal)).ToArray();

        // Guard against the fixture drifting: if nothing matched, the mutation proved nothing.
        Assert.True(kept.Length < lines.Length, $"No line contained '{needle}'; nothing was removed.");

        return string.Join('\n', kept);
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
