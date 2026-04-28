namespace SpaceSniffer.Services;

/// <summary>
/// Wraps an IProgress&lt;T&gt; and limits reports to at most once per minIntervalMs.
/// Thread-safe for concurrent callers (e.g. Parallel.ForEach).
/// </summary>
public class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly int _minIntervalMs;
    private long _lastReportTimestamp;

    public ThrottledProgress(IProgress<T> inner, int minIntervalMs = 200)
    {
        _inner = inner;
        _minIntervalMs = minIntervalMs;
    }

    public void Report(T value)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastReportTimestamp);

        if (last == 0 || now - last >= _minIntervalMs)
        {
            Interlocked.Exchange(ref _lastReportTimestamp, now);
            _inner.Report(value);
        }
    }
}
