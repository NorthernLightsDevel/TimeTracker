namespace TimeTracker.Domain.Dtos;

public sealed record class TimerCommandResultDto(
    TimerCommandStatus Status,
    TimerSessionSnapshotDto Snapshot,
    string Message);

