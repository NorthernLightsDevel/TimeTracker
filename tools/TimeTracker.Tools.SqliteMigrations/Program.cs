using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TimeTracker.Persistence;

namespace TimeTracker.Tools.SqliteMigrations;

public sealed class SqliteMigrationDesignTimeFactory : IDesignTimeDbContextFactory<TimeTrackerDbContext>
{
    private const string DatabaseFileName = "timetracker.tools.sqlite.db";

    public TimeTrackerDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(AppContext.BaseDirectory, DatabaseFileName);

        var optionsBuilder = new DbContextOptionsBuilder<TimeTrackerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={databasePath}", sqliteOptions =>
        {
            sqliteOptions.MigrationsAssembly(TimeTrackerDbContext.SqliteMigrationsAssembly);
        });

        return new TimeTrackerDbContext(optionsBuilder.Options);
    }
}

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        Console.WriteLine("TimeTracker Sqlite migrations tool is intended for use with dotnet ef.");
        return Task.FromResult(0);
    }
}
