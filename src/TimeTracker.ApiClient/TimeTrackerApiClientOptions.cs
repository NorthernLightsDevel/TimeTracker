using System;

namespace TimeTracker.ApiClient;

public sealed class TimeTrackerApiClientOptions
{
    public const string DefaultBaseAddress = "http://127.0.0.1:5058/";

    public string BaseAddress { get; set; } = DefaultBaseAddress;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
