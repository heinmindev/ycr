namespace YCR.Infrastructure.Tests.Persistence;

public sealed class CiWorkflowTests
{
    [Theory]
    [InlineData("build-and-test", 45)]
    [InlineData("api-smoke", 30)]
    [InlineData("trunk-only-tests", 60)]
    public void WorkflowJob_WithMigrationBundleRuntime_ProvidesDesignTimeConnection(
        string jobName,
        int timeoutMinutes)
    {
        var workflow = ReadWorkflow();
        var jobStart = workflow.IndexOf($"  {jobName}:", StringComparison.Ordinal);
        Assert.True(jobStart >= 0, jobName);

        var nextJob = FindNextJob(workflow, jobStart + 1);
        var jobBlock = nextJob >= 0
            ? workflow[jobStart..nextJob]
            : workflow[jobStart..];

        Assert.Contains(
            $"    timeout-minutes: {timeoutMinutes}\n    env:\n      YCR_DESIGN_TIME_CONNECTION:",
            jobBlock,
            StringComparison.Ordinal);
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

    private static int FindNextJob(string workflow, int start)
    {
        var searchStart = start;
        while (true)
        {
            var candidate = workflow.IndexOf("\n  ", searchStart, StringComparison.Ordinal);
            if (candidate < 0 || candidate + 3 >= workflow.Length || workflow[candidate + 3] != ' ')
            {
                return candidate;
            }

            searchStart = candidate + 3;
        }
    }
}
