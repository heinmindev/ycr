namespace YCR.Application.Reporting
{
    public interface IReportingWriteContext;
}

namespace YCR.Application.Reporting.Violations
{
    public sealed class ReportingUsingWriteContext
    {
        public YCR.Application.Reporting.IReportingWriteContext Context { get; } = null!;
    }
}
