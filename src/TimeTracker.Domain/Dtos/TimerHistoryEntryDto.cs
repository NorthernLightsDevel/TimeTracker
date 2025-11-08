namespace TimeTracker.Domain.Dtos;

public sealed record class TimerHistoryEntryDto(
    Guid TimeEntryId,
    Guid CustomerId,
    string CustomerName,
    Guid ProjectId,
    string ProjectName,
    DateTime StartLocal,
    DateTime EndLocal,
    TimeSpan Duration,
    TimeSpan RoundedDuration,
    bool Billable,
    string Notes,
    string Tag);
