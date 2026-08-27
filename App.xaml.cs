using System.Windows;
using Come.Services;
using Come.ViewModels;

namespace Come;

public partial class App : Application
{
    private IdleTimerService? _idleTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainViewModel(
            new PartCatalogService(),
            new TechFuelCatalogService(),
            new CompatibilityService(),
            new BuildStorageService(),
            new DemoPaymentService());

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        _idleTimer = new IdleTimerService(window, TimeSpan.FromSeconds(60), viewModel.ReturnToAttract);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _idleTimer?.Dispose();
        base.OnExit(e);
    }
}
