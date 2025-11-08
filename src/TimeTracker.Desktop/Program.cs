using System;
using Avalonia;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTracker.ApiClient;
using TimeTracker.Application.Reporting;
using TimeTracker.Desktop.Infrastructure;
using TimeTracker.Desktop.ProjectManagement;
using TimeTracker.Desktop.Reporting;

namespace TimeTracker.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var host = CreateHostBuilder(args).Build();
        App.ConfigureServices(host.Services);

        host.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            })
            .ConfigureLogging(builder =>
            {
                builder.ClearProviders();
#if DEBUG
                builder.AddDebug();
#endif
                builder.AddConsole();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
                services.AddTimeTrackerApiClient(options =>
                {
                    hostContext.Configuration.GetSection("TimeTracker:Api").Bind(options);
                });
                services.AddScoped<ITimeReportExporter, TimeReportExporter>();

                services.AddScoped<MainViewModel>();
                services.AddScoped<ProjectManagementViewModel>();
                services.AddScoped<DailyReportViewModel>();
            });

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
