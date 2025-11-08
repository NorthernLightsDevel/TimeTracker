using System;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Domain.Entities;
using TimeTracker.Persistence.Configurations;

namespace TimeTracker.Persistence;

public sealed class TimeTrackerDbContext : DbContext
{
    public TimeTrackerDbContext(DbContextOptions<TimeTrackerDbContext> options)
        : base(options)
    {
    }

    public const string SqliteMigrationsAssembly = "TimeTracker.Persistence.SqliteMigrations";
    public const string PgSqlMigrationsAssembly = "TimeTracker.Persistence.PgSqlMigrations";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());

        if (string.Equals(Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            modelBuilder.Entity<TimeEntry>(builder =>
            {
                builder.Property(entry => entry.StartLocal)
                    .HasColumnType("timestamp without time zone");

                builder.Property(entry => entry.EndLocal)
                    .HasColumnType("timestamp without time zone");
            });
        }
    }
}
