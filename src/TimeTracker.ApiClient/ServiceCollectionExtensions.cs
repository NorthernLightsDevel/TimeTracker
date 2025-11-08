using System;
using Microsoft.Extensions.DependencyInjection;
using TimeTracker.ApiClient.Repositories;
using TimeTracker.ApiClient.Services;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Services;

namespace TimeTracker.ApiClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimeTrackerApiClient(
        this IServiceCollection services,
        Action<TimeTrackerApiClientOptions> configure = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var optionsBuilder = services.AddOptions<TimeTrackerApiClientOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.PostConfigure(options =>
        {
            if (string.IsNullOrWhiteSpace(options.BaseAddress))
            {
                options.BaseAddress = TimeTrackerApiClientOptions.DefaultBaseAddress;
            }

            if (options.Timeout <= TimeSpan.Zero)
            {
                options.Timeout = TimeSpan.FromSeconds(10);
            }
        });

        services.AddHttpClient<TimeTrackerApiHttpClient>();
        services.AddSingleton(TimeProvider.System);

        services.AddTransient<ITimerSessionService, ApiTimerSessionService>();
        services.AddTransient<ICustomerRepository, ApiCustomerRepository>();
        services.AddTransient<IProjectRepository, ApiProjectRepository>();
        services.AddTransient<IProjectService, ApiProjectService>();

        return services;
    }

    public static IServiceCollection AddTimeTrackerApiClient(
        this IServiceCollection services,
        TimeTrackerApiClientOptions options)
    {
        if (options is null)
        {
            return services.AddTimeTrackerApiClient();
        }

        return services.AddTimeTrackerApiClient(config =>
        {
            config.BaseAddress = options.BaseAddress;
            config.Timeout = options.Timeout;
        });
    }
}
