using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using TimeTracker.ApiClient;
using TimeTracker.Application.Reporting;

namespace TimeTracker.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        using var host = CreateHostBuilder(args).Build();

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<CommandExecutor>();
            return await executor.ExecuteAsync(args, cancellationSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 130; // POSIX signal 2
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
                builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled);
                builder.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddTimeTrackerApiClient(options =>
                {
                    context.Configuration.GetSection("TimeTracker:Api").Bind(options);
                });
                services.AddScoped<ITimeReportExporter, TimeReportExporter>();
                services.AddScoped<CommandExecutor>();
            });
}
