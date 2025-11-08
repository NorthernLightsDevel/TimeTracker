namespace TimeTracker.Domain.Dtos;

public sealed record class TimerSessionSnapshotDto(
    TimerSessionDtos Status,
    ActiveTimerSessionDto ActiveSession,
    DateOnly LocalDate,
    IReadOnlyList<TimerHistoryEntryDto> Entries);

