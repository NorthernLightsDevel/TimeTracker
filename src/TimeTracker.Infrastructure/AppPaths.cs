using System;
using System.IO;

namespace TimeTracker.Infrastructure;

public static class AppPaths
{
    private const string AppFolderName = "TimeTracker";
    private const string DatabaseFileName = "timetracker.db";

    public static string EnsureDatabasePath()
    {
        var directory = EnsureAppDataDirectory();
        return Path.Combine(directory, DatabaseFileName);
    }

    public static string EnsureAppDataDirectory()
    {
        var baseDirectory = GetBaseApplicationDataDirectory();
        var appDirectory = Path.Combine(baseDirectory, AppFolderName);
        Directory.CreateDirectory(appDirectory);
        return appDirectory;
    }

    private static string GetBaseApplicationDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return localAppData;
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return xdgDataHome;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new InvalidOperationException("Unable to determine a writable application data directory.");
        }

        return Path.Combine(homeDirectory, ".local", "share");
    }
}
