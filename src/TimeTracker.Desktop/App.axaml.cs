using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace TimeTracker.Desktop;

public partial class App : Avalonia.Application
{
    private static IServiceProvider _services = null!;
    private static IServiceScope _applicationScope;

    public static void ConfigureServices(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public static IServiceProvider Services => _services ?? throw new InvalidOperationException("Services have not been configured.");

    public static IServiceScope CreateScope()
    {
        if (_services is null)
        {
            throw new InvalidOperationException("Services have not been configured.");
        }

        return _services.CreateScope();
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _applicationScope = _services.CreateScope();
            var mainViewModel = _applicationScope.ServiceProvider.GetRequiredService<MainViewModel>();

            desktop.Exit += OnDesktopExit;

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDesktopExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _applicationScope?.Dispose();
        _applicationScope = null;
    }
}
