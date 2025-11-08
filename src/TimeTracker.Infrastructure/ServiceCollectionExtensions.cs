using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Services;
using TimeTracker.Persistence;

namespace TimeTracker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimeTrackerCore(
        this IServiceCollection services,
        Action<TimeTrackerDatabaseOptions> configure = null)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var databaseOptions = new TimeTrackerDatabaseOptions();
        configure?.Invoke(databaseOptions);

        services.AddOptions();
        services.AddSingleton(TimeProvider.System);

        switch (databaseOptions.Provider)
        {
            case TimeTrackerDatabaseProvider.Sqlite:
                ConfigureSqlite(services, databaseOptions);
                break;
            case TimeTrackerDatabaseProvider.PostgreSql:
                ConfigurePostgreSql(services, databaseOptions);
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider '{databaseOptions.Provider}'.");
        }

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<ITimerSessionService, TimerSessionService>();
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }

    private static void ConfigureSqlite(IServiceCollection services, TimeTrackerDatabaseOptions options)
    {
        var databasePath = options.DatabasePath;
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = AppPaths.EnsureDatabasePath();
        }
        else
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var connectionString = options.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = $"Data Source={databasePath};Cache=Shared;Mode=ReadWriteCreate;Default Timeout=5";
        }

        services.AddDbContext<TimeTrackerDbContext>(builder =>
        {
            builder.UseSqlite(connectionString, sqlite =>
            {
                sqlite.MigrationsAssembly(TimeTrackerDbContext.SqliteMigrationsAssembly);
            });

            ApplyDebugOptions(builder);
        });
    }

    private static void ConfigurePostgreSql(IServiceCollection services, TimeTrackerDatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("PostgreSQL provider requires a connection string.");
        }

        services.AddDbContext<TimeTrackerDbContext>(builder =>
        {
            builder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(TimeTrackerDbContext.PgSqlMigrationsAssembly);
            });

            ApplyDebugOptions(builder);
        });
    }

    private static void ApplyDebugOptions(DbContextOptionsBuilder builder)
    {
#if DEBUG
        builder.EnableDetailedErrors();
        builder.EnableSensitiveDataLogging();
#endif
    }
}
