using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Services;

public interface ITimerSessionService
{
    Task<TimerSessionSnapshotDto> GetSnapshotAsync(
        DateOnly? localDate = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimerHistoryEntryDto>> GetHistoryAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimerDailySummaryDto>> GetDailySummaryAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> StartAsync(
        TimerSessionStartOptions options,
        CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> PauseAsync(CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> ResumeAsync(CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> StopAsync(
        TimerSessionStopOptions options,
        CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> CancelAsync(CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> UpdateNotesAsync(
        string notes,
        CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> AdjustEntryAsync(
        TimeEntryAdjustmentOptions options,
        CancellationToken cancellationToken = default);

    Task<TimerCommandResultDto> DeleteEntryAsync(
        Guid timeEntryId,
        CancellationToken cancellationToken = default);
}
