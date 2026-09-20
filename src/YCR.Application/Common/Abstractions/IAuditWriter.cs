namespace YCR.Application.Common.Abstractions;

public interface IAuditWriter
{
    void Record(
        string action,
        object subject,
        object? before,
        object? after,
        string? authorizedByPermission = null,
        string? reasonCode = null);
}
