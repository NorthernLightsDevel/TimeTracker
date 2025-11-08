namespace TimeTracker.Infrastructure;

public enum TimeTrackerDatabaseProvider
{
    Sqlite,
    PostgreSql
}

public sealed class TimeTrackerDatabaseOptions
{
    public TimeTrackerDatabaseProvider Provider { get; set; } = TimeTrackerDatabaseProvider.Sqlite;

    public string ConnectionString { get; set; }

    public string DatabasePath { get; set; }
}
