using System;
using System.Threading;

namespace ShotLog.Infrastructure;

/// <summary>
/// Ensures a single running instance via a named <see cref="Mutex"/>, and lets a
/// second launch wake the first one through a named <see cref="EventWaitHandle"/>. Ported from OrbitDock.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly bool _owns;
    private readonly Thread? _listener;
    private volatile bool _running = true;

    public bool IsFirstInstance { get; }

    /// <summary>Raised on the first instance when a second instance requests attention.</summary>
    public event EventHandler? SecondInstanceRequested;

    public SingleInstance(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, name + ".mutex", out bool createdNew);
        IsFirstInstance = createdNew;
        _owns = createdNew;
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".signal");

        if (IsFirstInstance)
        {
            _listener = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "ShotLog.SingleInstanceListener",
            };
            _listener.Start();
        }
    }

    private void ListenLoop()
    {
        while (_running)
        {
            if (_signal.WaitOne(500) && _running)
                SecondInstanceRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SignalFirstInstance() => _signal.Set();

    public void Dispose()
    {
        _running = false;
        try { _signal.Set(); } catch { /* waking the listener to exit */ }
        _listener?.Join(1000);
        _signal.Dispose();
        if (_owns)
        {
            try { _mutex.ReleaseMutex(); } catch { /* not owned anymore */ }
        }
        _mutex.Dispose();
    }
}
