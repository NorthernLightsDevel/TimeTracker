using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Testcontainers.PostgreSql;
using TimeTracker.Persistence;

namespace TimeTracker.Tools.PgSqlMigrations;

public sealed class PgSqlMigrationDesignTimeFactory : IDesignTimeDbContextFactory<TimeTrackerDbContext>
{
    private const string DatabaseName = "timetracker";
    private const string Username = "postgres";
    private const string Password = "postgres";

    private static readonly object SyncRoot = new();
    private static PostgreSqlContainer _container;

    public TimeTrackerDbContext CreateDbContext(string[] args)
    {
        EnsureContainerStarted();

        var builder = new DbContextOptionsBuilder<TimeTrackerDbContext>();
        builder.UseNpgsql(GetConnectionString(), npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(TimeTrackerDbContext.PgSqlMigrationsAssembly);
        });

        return new TimeTrackerDbContext(builder.Options);
    }

    private static void EnsureContainerStarted()
    {
        if (_container is not null)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_container is not null)
            {
                return;
            }

            _container = new PostgreSqlBuilder()
                .WithDatabase(DatabaseName)
                .WithUsername(Username)
                .WithPassword(Password)
                .Build();

            _container.StartAsync().GetAwaiter().GetResult();

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (_container is null)
                {
                    return;
                }

                _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _container = null;
            };
        }
    }

    private static string GetConnectionString()
    {
        if (_container is null)
        {
            throw new InvalidOperationException("PostgreSQL testcontainer not initialized.");
        }

        return _container.GetConnectionString();
    }
}

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        Console.WriteLine("TimeTracker PostgreSQL migrations tool is intended for use with dotnet ef.");
        return Task.FromResult(0);
    }
}
