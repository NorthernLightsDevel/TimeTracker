namespace TimeTracker.Application.Services;

public sealed record class TimerSessionStopOptions(
    string Notes = null,
    bool? Billable = null,
    string Tag = null,
    DateTime? StopLocalOverride = null,
    bool PersistEmptyEntry = false);
