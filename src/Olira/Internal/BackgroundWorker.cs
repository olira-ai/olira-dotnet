#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Olira.Json;

namespace Olira.Internal;

/// <summary>
/// Daemon thread that drains a bounded queue, batches log entries, and sends via <c>sendBatch</c>.
/// <see cref="Flush"/> blocks until the queue is empty and the in-flight batch is done.
/// </summary>
internal sealed class BackgroundWorker : IDisposable
{
    private readonly Action<IReadOnlyList<object>> _sendBatch;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly object _onError;
    private readonly BlockingCollection<LogWire?> _queue;
    private readonly object _lock = new();
    private readonly List<LogWire> _pending = [];
    private readonly CancellationTokenSource _shutdown = new();
    private Thread? _thread;
    private bool _closed;
    private bool _atexitRegistered;

    /// <summary>
    /// Creates a background worker.
    /// </summary>
    /// <param name="sendBatch">Callback that posts a batch of wire-format log dicts/objects.</param>
    /// <param name="batchSize">Flush when this many events are pending (default 50).</param>
    /// <param name="flushInterval">Max time between flushes (default 1.5s).</param>
    /// <param name="maxQueueSize">Bounded queue capacity (default 10_000).</param>
    /// <param name="onError">
    /// <c>"drop"</c> (default), <c>"raise"</c>, or a callback
    /// <c>Action&lt;Exception, IReadOnlyList&lt;string&gt;&gt;</c> receiving the error and affected log types.
    /// </param>
    public BackgroundWorker(
        Action<IReadOnlyList<object>> sendBatch,
        int batchSize = 50,
        double flushInterval = 1.5,
        int maxQueueSize = 10_000,
        object? onError = null)
    {
        _sendBatch = sendBatch ?? throw new ArgumentNullException(nameof(sendBatch));
        _batchSize = batchSize;
        _flushInterval = TimeSpan.FromSeconds(flushInterval);
        _onError = onError ?? "drop";
        _queue = new BlockingCollection<LogWire?>(new ConcurrentQueue<LogWire?>(), maxQueueSize);
    }

    /// <summary>Starts the daemon worker thread (idempotent).</summary>
    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "olira-background-worker",
        };
        _thread.Start();

        if (!_atexitRegistered)
        {
            _atexitRegistered = true;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (!_closed)
        {
            Flush();
        }
    }

    /// <summary>Enqueue one log entry. Returns false if the queue is full (entry dropped).</summary>
    public bool Enqueue(LogWire eventWire)
    {
        if (_queue.TryAdd(eventWire))
        {
            return true;
        }

        NotifyError(
            new InvalidOperationException("Event queue full; event dropped"),
            [eventWire.LogType]);
        return false;
    }

    private void NotifyError(Exception error, IReadOnlyList<string> logTypes)
    {
        switch (_onError)
        {
            case "drop":
                Debug.WriteLine($"Events dropped: {string.Join(", ", logTypes.Take(5))} ({error.Message})");
                break;
            case "raise":
                throw error;
            case Action<Exception, IReadOnlyList<string>> callback:
                callback(error, logTypes);
                break;
        }
    }

    private void Run()
    {
        var lastFlush = Stopwatch.GetTimestamp();
        while (!_shutdown.IsCancellationRequested)
        {
            var elapsed = Stopwatch.GetElapsedTime(lastFlush);
            var timeout = _flushInterval - elapsed;
            if (timeout < TimeSpan.FromMilliseconds(100))
            {
                timeout = TimeSpan.FromMilliseconds(100);
            }

            LogWire? item = null;
            var got = false;
            try
            {
                got = _queue.TryTake(out item, timeout);
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (got && item is not null)
            {
                lock (_lock)
                {
                    _pending.Add(item);
                }

                if (_pending.Count >= _batchSize)
                {
                    FlushPending();
                    lastFlush = Stopwatch.GetTimestamp();
                }

                continue;
            }

            lock (_lock)
            {
                if (_pending.Count > 0)
                {
                    FlushPending();
                }
            }

            lastFlush = Stopwatch.GetTimestamp();
        }

        lock (_lock)
        {
            if (_pending.Count > 0)
            {
                FlushPending();
            }
        }
    }

    private void FlushPending()
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = _pending.ToList();
        _pending.Clear();
        try
        {
            var payloads = batch.Select(ToWireObject).ToList();
            _sendBatch(payloads);
        }
        catch (Exception ex)
        {
            NotifyError(ex, batch.Select(e => e.LogType).ToList());
        }
    }

    private static object ToWireObject(LogWire wire)
    {
        // Include null optional fields — parity with Python model_dump(mode="json") on the queue path.
        var json = JsonSerializer.Serialize(wire, OliraJson.IncludeNulls);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, OliraJson.IncludeNulls)!;
    }

    /// <summary>Block until the queue is empty and the current batch is sent.</summary>
    public void Flush()
    {
        if (_thread is null)
        {
            return;
        }

        while (_queue.TryTake(out var item))
        {
            if (item is null)
            {
                break;
            }

            lock (_lock)
            {
                _pending.Add(item);
            }
        }

        lock (_lock)
        {
            if (_pending.Count > 0)
            {
                FlushPending();
            }
        }
    }

    /// <summary>Stop the worker and flush remaining events.</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        if (_atexitRegistered)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            _atexitRegistered = false;
        }

        _shutdown.Cancel();
        _queue.CompleteAdding();

        if (_thread is not null)
        {
            if (!_thread.Join(TimeSpan.FromSeconds(5)))
            {
                Debug.WriteLine("olira background worker did not stop within 5s");
            }

            _thread = null;
        }

        lock (_lock)
        {
            if (_pending.Count > 0)
            {
                FlushPending();
            }
        }

        _queue.Dispose();
        _shutdown.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => Close();
}
