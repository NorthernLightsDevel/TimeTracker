namespace TimeTracker.Domain.Dtos;

public enum TimerCommandStatus
{
    Success = 0,
    ValidationFailed = 1,
    Conflict = 2,
    NotFound = 3,
    Failure = 4
}

