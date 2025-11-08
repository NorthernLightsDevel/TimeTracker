using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TimeTracker.Application.Repositories;
using TimeTracker.Persistence;

namespace TimeTracker.Application.Tests.Infrastructure;

public enum DatabaseProvider
{
    Sqlite,
    PgSql
}

public sealed class DatabaseHarness : IAsyncDisposable
{
    private readonly DatabaseProvider _provider;
    private readonly PostgreSqlContainer _pgSqlContainer;
    private readonly string _sqlitePath;

    private DatabaseHarness(
        DatabaseProvider provider,
        TimeTrackerDbContext context,
        PostgreSqlContainer pgSqlContainer,
        string sqlitePath)
    {
        _provider = provider;
        Context = context;
        _pgSqlContainer = pgSqlContainer;
        _sqlitePath = sqlitePath;

        CustomerRepository = new CustomerRepository(Context);
        ProjectRepository = new ProjectRepository(Context);
        TimeEntryRepository = new TimeEntryRepository(Context);
    }

    public TimeTrackerDbContext Context { get; }

    public ICustomerRepository CustomerRepository { get; }

    public IProjectRepository ProjectRepository { get; }

    public ITimeEntryRepository TimeEntryRepository { get; }

    public static async Task<DatabaseHarness> CreateAsync(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.Sqlite => await CreateSqliteAsync(),
            DatabaseProvider.PgSql => await CreatePgSqlAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    private static async Task<DatabaseHarness> CreateSqliteAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"timetracker-tests-{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseSqlite($"Data Source={databasePath}", builder =>
            {
                builder.MigrationsAssembly(TimeTrackerDbContext.SqliteMigrationsAssembly);
            })
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TimeTrackerDbContext(options);
        await context.Database.MigrateAsync();

        return new DatabaseHarness(DatabaseProvider.Sqlite, context, null, databasePath);
    }

    private static async Task<DatabaseHarness> CreatePgSqlAsync()
    {
        var container = new PostgreSqlBuilder()
            .WithDatabase("timetracker_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await container.StartAsync();

        var connectionString = container.GetConnectionString();

        var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
            .UseNpgsql(connectionString, builder =>
            {
                builder.MigrationsAssembly(TimeTrackerDbContext.PgSqlMigrationsAssembly);
            })
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TimeTrackerDbContext(options);
        await context.Database.MigrateAsync();

        return new DatabaseHarness(DatabaseProvider.PgSql, context, container, null);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();

        switch (_provider)
        {
            case DatabaseProvider.Sqlite when _sqlitePath is not null && File.Exists(_sqlitePath):
                File.Delete(_sqlitePath);
                break;
            case DatabaseProvider.PgSql when _pgSqlContainer is not null:
                await _pgSqlContainer.DisposeAsync();
                break;
        }
    }
}
