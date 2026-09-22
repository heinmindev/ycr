namespace YCR.TestSupport;

/// <summary>
/// An exclusive lock held across operating-system processes, backed by a lock file.
/// </summary>
/// <remarks>
/// A named <see cref="Mutex"/> would be simpler but is not shared between processes on Linux,
/// where CI runs. An exclusively opened file is honoured by every platform, and the operating
/// system releases the handle even if a test process is killed, so a crash cannot leave the lock
/// held forever.
/// </remarks>
internal sealed class CrossProcessLock : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly FileStream _stream;

    private CrossProcessLock(FileStream stream) => _stream = stream;

    public static async Task<CrossProcessLock> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (true)
        {
            try
            {
                return new CrossProcessLock(new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose));
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                // Another process holds it. The wait is expected, not exceptional: building the
                // migration bundle takes tens of seconds and every test process needs it.
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                throw new TimeoutException(
                    $"Waited {Timeout.TotalMinutes:0} minutes for the lock at '{path}'. "
                    + "A previous test run may have left a process alive.",
                    exception);
            }
        }
    }

    public void Dispose() => _stream.Dispose();
}
