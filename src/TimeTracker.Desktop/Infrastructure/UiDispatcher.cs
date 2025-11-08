using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace TimeTracker.Desktop.Infrastructure;

/// <summary>
/// Provides access to the UI dispatcher so view models can marshal work and timers onto the UI thread.
/// </summary>
public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action action);

    Task InvokeAsync(Action action);

    DispatcherTimer CreateTimer(TimeSpan interval, EventHandler tick, bool start = false);
}

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Dispatcher.UIThread.Post(action);
    }

    public async Task InvokeAsync(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        await Dispatcher.UIThread.InvokeAsync(action).GetTask().ConfigureAwait(false);
    }

    public DispatcherTimer CreateTimer(TimeSpan interval, EventHandler tick, bool start = false)
    {
        if (tick is null)
        {
            throw new ArgumentNullException(nameof(tick));
        }

        var timer = new DispatcherTimer
        {
            Interval = interval
        };

        timer.Tick += tick;

        if (start)
        {
            timer.Start();
        }

        return timer;
    }
}
