namespace TimeTracker.Application.Services;

public sealed record class TimerSessionStartOptions(
    Guid ProjectId,
    Guid? CustomerId = null,
    string Notes = null,
    bool Billable = true,
    string Tag = null,
    DateTime? StartLocalOverride = null,
    bool ForceRestart = false);
