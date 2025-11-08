namespace TimeTracker.Domain.Dtos;

public sealed record class TimerDailySummaryDto(
    DateOnly LocalDate,
    TimeSpan TotalDuration,
    TimeSpan TotalRoundedDuration,
    IReadOnlyList<TimerHistoryEntryDto> Entries);

