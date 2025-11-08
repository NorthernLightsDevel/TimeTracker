namespace TimeTracker.Domain.Dtos;

public sealed record class ActiveTimerSessionDto(
    Guid TimeEntryId,
    Guid CustomerId,
    Guid ProjectId,
    string CustomerName,
    string ProjectName,
    DateTime StartLocal,
    DateTime StartUtc,
    DateTime LastInteractionUtc,
    TimeSpan AccumulatedDuration,
    TimeSpan RoundedDuration,
    bool IsPaused,
    string Notes,
    bool Billable,
    string Tag);

