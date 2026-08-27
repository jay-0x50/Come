using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Come.Services;

public sealed class IdleTimerService : IDisposable
{
    private readonly Window _window;
    private readonly DispatcherTimer _timer;
    private readonly Action _onTimeout;

    public IdleTimerService(Window window, TimeSpan timeout, Action onTimeout)
    {
        _window = window;
        _onTimeout = onTimeout;
        _timer = new DispatcherTimer { Interval = timeout };
        _timer.Tick += OnTick;
        _window.PreviewMouseDown += OnActivity;
        _window.PreviewTouchDown += OnActivity;
        _window.PreviewKeyDown += OnActivity;
        _timer.Start();
    }

    private void OnActivity(object? sender, InputEventArgs e)
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _onTimeout();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _window.PreviewMouseDown -= OnActivity;
        _window.PreviewTouchDown -= OnActivity;
        _window.PreviewKeyDown -= OnActivity;
    }
}
