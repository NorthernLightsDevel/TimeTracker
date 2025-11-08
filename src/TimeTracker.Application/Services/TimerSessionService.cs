using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Application.Repositories;
using TimeTracker.Domain.Dtos;
using TimeTracker.Domain.Utilities;

namespace TimeTracker.Application.Services;

public sealed class TimerSessionService : ITimerSessionService, IDisposable
{
    private const int QuarterMinutes = 15;

    private readonly ITimeEntryRepository _timeEntries;
    private readonly IProjectRepository _projects;
    private readonly ICustomerRepository _customers;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private SessionState _sessionState;

    public TimerSessionService(
            ITimeEntryRepository timeEntries,
            IProjectRepository projects,
            ICustomerRepository customers,
            TimeProvider timeProvider = null,
            TimeZoneInfo timeZone = null)
    {
        _timeEntries = timeEntries ?? throw new ArgumentNullException(nameof(timeEntries));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task<TimerSessionSnapshotDto> GetSnapshotAsync(
            DateOnly? localDate = null,
            CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return await BuildSnapshotInternalAsync(localDate, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<TimerHistoryEntryDto>> GetHistoryAsync(
            DateOnly localDate,
            CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var entries = await _timeEntries.GetByLocalDateAsync(localDate, cancellationToken);
            var projectCache = new Dictionary<Guid, ProjectDto>();
            var customerCache = new Dictionary<Guid, CustomerDto>();
            var results = new List<TimerHistoryEntryDto>(entries.Count);

            foreach (var entry in entries)
            {
                var historyEntry = await TryCreateHistoryEntryAsync(entry, projectCache, customerCache, cancellationToken);
                if (historyEntry is not null)
                {
                    results.Add(historyEntry);
                }
            }

            return results;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<TimerDailySummaryDto>> GetDailySummaryAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be greater than or equal to start date.", nameof(endDate));
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var projectCache = new Dictionary<Guid, ProjectDto>();
            var customerCache = new Dictionary<Guid, CustomerDto>();
            var summaries = new List<TimerDailySummaryDto>();
            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);

            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                var entries = await _timeEntries.GetByLocalDateAsync(currentDate, cancellationToken);
                var historyEntries = new List<TimerHistoryEntryDto>(entries.Count);
                var totalDuration = TimeSpan.Zero;
                var perProjectDurations = new Dictionary<Guid, TimeSpan>();

                foreach (var entry in entries)
                {
                    var historyEntry = await TryCreateHistoryEntryAsync(entry, projectCache, customerCache, cancellationToken);
                    if (historyEntry is null)
                    {
                        continue;
                    }

                    historyEntries.Add(historyEntry);
                    totalDuration += historyEntry.Duration;
                    AccumulateDuration(perProjectDurations, historyEntry.ProjectId, historyEntry.Duration);
                }

                if (activeEntry is not null && DateOnly.FromDateTime(activeEntry.StartLocal) == currentDate)
                {
                    var nowLocal = GetCurrentLocalTime();
                    var runningDuration = CalculateDuration(activeEntry.StartLocal, nowLocal);
                    var runningRounded = RoundDuration(runningDuration, allowZeroDuration: true);
                    var project = await GetProjectAsync(activeEntry.ProjectId, projectCache, cancellationToken);
                    var customer = await GetCustomerAsync(activeEntry.CustomerId, customerCache, cancellationToken);

                    var runningEntry = new TimerHistoryEntryDto(
                            activeEntry.Id,
                            activeEntry.CustomerId,
                            customer.Name,
                            activeEntry.ProjectId,
                            project.Name,
                            activeEntry.StartLocal,
                            nowLocal,
                            runningDuration,
                            runningRounded,
                            activeEntry.Billable,
                            activeEntry.Notes,
                            activeEntry.Tag ?? string.Empty);

                    historyEntries.Insert(0, runningEntry);
                    totalDuration += runningDuration;
                    AccumulateDuration(perProjectDurations, runningEntry.ProjectId, runningDuration);
                }

                if (historyEntries.Count > 0)
                {
                    var totalRoundedDuration = TimeSpan.Zero;
                    foreach (var duration in perProjectDurations.Values)
                    {
                        totalRoundedDuration += RoundDuration(duration, allowZeroDuration: true);
                    }

                    summaries.Add(new TimerDailySummaryDto(
                                currentDate,
                                totalDuration,
                                totalRoundedDuration,
                                historyEntries));
                }

                currentDate = currentDate.AddDays(1);
            }

            return summaries;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<TimerHistoryEntryDto> TryCreateHistoryEntryAsync(
            TimeEntryDto entry,
            IDictionary<Guid, ProjectDto> projectCache,
            IDictionary<Guid, CustomerDto> customerCache,
            CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return null;
        }

        if (!entry.EndLocal.HasValue || entry.IsDeleted)
        {
            return null;
        }

        var project = await GetProjectAsync(entry.ProjectId, projectCache, cancellationToken);
        var customer = await GetCustomerAsync(entry.CustomerId, customerCache, cancellationToken);
        var endLocal = entry.EndLocal.Value;
        var duration = CalculateDuration(entry.StartLocal, endLocal);
        var roundedDuration = RoundDuration(duration, allowZeroDuration: true);

        return new TimerHistoryEntryDto(
                entry.Id,
                entry.CustomerId,
                customer.Name,
                entry.ProjectId,
                project.Name,
                entry.StartLocal,
                endLocal,
                duration,
                roundedDuration,
                entry.Billable,
                entry.Notes,
                entry.Tag ?? string.Empty);
    }

    public async Task<TimerCommandResultDto> StartAsync(
            TimerSessionStartOptions options,
            CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (options.ProjectId == Guid.Empty)
            {
                return await CommandFailureAsync(TimerCommandStatus.ValidationFailed, "Project is required.", null, cancellationToken);
            }

            var project = await _projects.GetByIdAsync(options.ProjectId, cancellationToken);
            if (project is null)
            {
                return await CommandFailureAsync(TimerCommandStatus.NotFound, "Project not found.", null, cancellationToken);
            }

            if (!project.IsActive)
            {
                return await CommandFailureAsync(TimerCommandStatus.ValidationFailed, "Project is archived.", null, cancellationToken);
            }

            var customerId = options.CustomerId ?? project.CustomerId;
            if (customerId == Guid.Empty)
            {
                return await CommandFailureAsync(TimerCommandStatus.ValidationFailed, "Customer is required.", null, cancellationToken);
            }

            if (options.CustomerId.HasValue && options.CustomerId.Value != project.CustomerId)
            {
                return await CommandFailureAsync(TimerCommandStatus.ValidationFailed, "Project does not belong to the specified customer.", null, cancellationToken);
            }

            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            var startLocal = NormalizeLocal(options.StartLocalOverride ?? GetCurrentLocalTime());
            startLocal = EnsureLocalKind(startLocal);
            var startUtc = ConvertLocalToUtc(startLocal);

            if (activeEntry is not null)
            {
                if (!options.ForceRestart)
                {
                    return await CommandFailureAsync(
                            TimerCommandStatus.Conflict,
                            "A session is already running. Use ForceRestart to override.",
                            null,
                            cancellationToken);
                }

                await StopEntryInternalAsync(activeEntry, startLocal, isPause: false, cancellationToken: cancellationToken);
            }

            var created = await _timeEntries.CreateAsync(new TimeEntryCreateDto(
                        customerId,
                        project.Id,
                        startLocal,
                        startUtc,
                        options.Notes ?? string.Empty,
                        options.Billable,
                        options.Tag ?? string.Empty), cancellationToken);

            _sessionState = new SessionState(
                    project.Id,
                    customerId,
                    created.Notes,
                    created.Billable,
                    created.Tag ?? string.Empty,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    created.LastModifiedUtc,
                    null,
                    created.StartLocal,
                    created.StartUtc,
                    null,
                    null,
                    false);

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, $"Timer started for {project.Name}.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> PauseAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            if (activeEntry is null)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "No active session to pause.",
                        null,
                        cancellationToken);
            }

            var nowLocal = GetCurrentLocalTime();
            await StopEntryInternalAsync(activeEntry, nowLocal, isPause: true, cancellationToken: cancellationToken);

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Timer paused.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> AdjustEntryAsync(
            TimeEntryAdjustmentOptions options,
            CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var entry = await _timeEntries.GetByIdAsync(options.TimeEntryId, cancellationToken);
            if (entry is null || entry.IsDeleted)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "Time entry not found.",
                        null,
                        cancellationToken);
            }

            if (!entry.EndLocal.HasValue)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.ValidationFailed,
                        "Stop the active session before editing it.",
                        DateOnly.FromDateTime(entry.StartLocal),
                        cancellationToken);
            }

            var targetStartLocal = options.StartLocal.HasValue
                ? EnsureLocalKind(NormalizeLocal(options.StartLocal.Value))
                : entry.StartLocal;

            var targetEndLocal = options.EndLocal.HasValue
                ? EnsureLocalKind(NormalizeLocal(options.EndLocal.Value))
                : entry.EndLocal.Value;

            if (targetEndLocal <= targetStartLocal)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.ValidationFailed,
                        "End time must be later than the start time.",
                        DateOnly.FromDateTime(targetStartLocal),
                        cancellationToken);
            }

            var startChanged = options.StartLocal.HasValue && targetStartLocal != entry.StartLocal;
            var endChanged = options.EndLocal.HasValue && entry.EndLocal.HasValue && targetEndLocal != entry.EndLocal.Value;
            var notesChanged = options.Notes is not null;

            if (!startChanged && !endChanged && !notesChanged)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.ValidationFailed,
                        "No changes detected.",
                        DateOnly.FromDateTime(targetStartLocal),
                        cancellationToken);
            }

            var updateDto = new TimeEntryUpdateDto(
                    entry.Id,
                    startChanged ? targetStartLocal : null,
                    startChanged ? ConvertLocalToUtc(targetStartLocal) : null,
                    endChanged ? targetEndLocal : null,
                    endChanged ? ConvertLocalToUtc(targetEndLocal) : null,
                    notesChanged ? options.Notes : null,
                    null,
                    null,
                    null,
                    null,
                    null);

            var updated = await _timeEntries.UpdateAsync(updateDto, cancellationToken);
            if (updated is null)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.Failure,
                        "Failed to update the selected time entry.",
                        DateOnly.FromDateTime(targetStartLocal),
                        cancellationToken);
            }

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Time entry updated.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> DeleteEntryAsync(
            Guid timeEntryId,
            CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var entry = await _timeEntries.GetByIdAsync(timeEntryId, cancellationToken);
            if (entry is null || entry.IsDeleted)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "Time entry not found.",
                        null,
                        cancellationToken);
            }

            if (!entry.EndLocal.HasValue)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.ValidationFailed,
                        "Stop the active session before deleting it.",
                        DateOnly.FromDateTime(entry.StartLocal),
                        cancellationToken);
            }

            var snapshotDate = DateOnly.FromDateTime(entry.StartLocal);
            var deleted = await _timeEntries.DeleteAsync(entry.Id, cancellationToken);

            if (!deleted)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.Failure,
                        "Failed to delete the selected time entry.",
                        snapshotDate,
                        cancellationToken);
            }

            if (_sessionState is not null && _sessionState.LastEntryId == entry.Id)
            {
                _sessionState = null;
            }

            var snapshot = await BuildSnapshotInternalAsync(snapshotDate, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Time entry deleted.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            if (activeEntry is not null)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.Conflict,
                        "A session is already running.",
                        null,
                        cancellationToken);
            }

            if (_sessionState is null || !_sessionState.IsPaused)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "No paused session to resume.",
                        null,
                        cancellationToken);
            }

            var project = await _projects.GetByIdAsync(_sessionState.ProjectId, cancellationToken);
            if (project is null)
            {
                _sessionState = null;
                return await CommandFailureAsync(TimerCommandStatus.NotFound, "Project no longer exists.", null, cancellationToken);
            }

            var startLocal = GetCurrentLocalTime();
            startLocal = EnsureLocalKind(startLocal);

            if (_sessionState.LastEndLocal.HasValue)
            {
                var lastEndLocal = EnsureLocalKind(_sessionState.LastEndLocal.Value);
                if (startLocal < lastEndLocal)
                {
                    startLocal = lastEndLocal;
                }
            }
            var startUtc = ConvertLocalToUtc(startLocal);

            var created = await _timeEntries.CreateAsync(new TimeEntryCreateDto(
                        _sessionState.CustomerId,
                        project.Id,
                        startLocal,
                        startUtc,
                        _sessionState.Notes,
                        _sessionState.Billable,
                        _sessionState.Tag), cancellationToken);

            _sessionState = _sessionState with
            {
                LastInteractionUtc = created.LastModifiedUtc,
                LastStartLocal = created.StartLocal,
                LastStartUtc = created.StartUtc,
                LastEndLocal = null,
                LastEndUtc = null,
                LastEntryId = created.Id,
                IsPaused = false
            };

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, $"Timer resumed for {project.Name}.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> StopAsync(
            TimerSessionStopOptions options,
            CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            if (activeEntry is null)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "No active session to stop.",
                        null,
                        cancellationToken);
            }

            var stopLocalCandidate = options?.StopLocalOverride ?? GetCurrentLocalTime();
            await StopEntryInternalAsync(
                    activeEntry,
                    stopLocalCandidate,
                    isPause: false,
                    cancellationToken: cancellationToken,
                    options: options);

            _sessionState = null;

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Timer stopped.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> UpdateNotesAsync(
            string notes,
            CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var noteValue = notes ?? string.Empty;

            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            if (activeEntry is not null)
            {
                var updateDto = new TimeEntryUpdateDto(
                        activeEntry.Id,
                        null,
                        null,
                        null,
                        null,
                        noteValue,
                        null,
                        null,
                        null,
                        null,
                        null);

                var updated = await _timeEntries.UpdateAsync(updateDto, cancellationToken);
                if (updated is null)
                {
                    return await CommandFailureAsync(
                            TimerCommandStatus.Failure,
                            "Failed to update notes for the active session.",
                            null,
                            cancellationToken);
                }

                if (_sessionState is not null)
                {
                    _sessionState = _sessionState with
                    {
                        Notes = noteValue,
                        LastInteractionUtc = updated.LastModifiedUtc
                    };
                }

                var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
                return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Notes updated for the active session.");
            }

            if (_sessionState is not null && _sessionState.IsPaused)
            {
                if (_sessionState.LastEntryId.HasValue)
                {
                    var updateDto = new TimeEntryUpdateDto(
                            _sessionState.LastEntryId.Value,
                            null,
                            null,
                            null,
                            null,
                            noteValue,
                            null,
                            null,
                            null,
                            null,
                            null);

                    var updated = await _timeEntries.UpdateAsync(updateDto, cancellationToken);
                    if (updated is null)
                    {
                        return await CommandFailureAsync(
                                TimerCommandStatus.Failure,
                                "Failed to update notes for the paused session.",
                                null,
                                cancellationToken);
                    }

                    _sessionState = _sessionState with
                    {
                        Notes = noteValue,
                        LastInteractionUtc = updated.LastModifiedUtc
                    };
                }
                else
                {
                    _sessionState = _sessionState with { Notes = noteValue };
                }

                var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
                return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Notes updated for the paused session.");
            }

            return await CommandFailureAsync(
                    TimerCommandStatus.NotFound,
                    "No active or paused session to update notes for.",
                    null,
                    cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<TimerCommandResultDto> CancelAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
            if (activeEntry is null)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.NotFound,
                        "No active session to cancel.",
                        null,
                        cancellationToken);
            }

            var deleted = await _timeEntries.DeleteAsync(activeEntry.Id, cancellationToken);
            _sessionState = null;

            if (!deleted)
            {
                return await CommandFailureAsync(
                        TimerCommandStatus.Failure,
                        "Failed to cancel the active session.",
                        null,
                        cancellationToken);
            }

            var snapshot = await BuildSnapshotInternalAsync(null, cancellationToken);
            return new TimerCommandResultDto(TimerCommandStatus.Success, snapshot, "Active session cancelled.");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }

    private async Task<TimerCommandResultDto> CommandFailureAsync(
            TimerCommandStatus status,
            string message,
            DateOnly? snapshotDate,
            CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotInternalAsync(snapshotDate, cancellationToken);
        return new TimerCommandResultDto(status, snapshot, message);
    }

    private async Task<TimerSessionSnapshotDto> BuildSnapshotInternalAsync(
            DateOnly? localDate,
            CancellationToken cancellationToken)
    {
        var targetDate = localDate ?? DateOnly.FromDateTime(GetCurrentLocalTime());
        var activeEntry = await _timeEntries.GetActiveAsync(cancellationToken);
        var entries = await _timeEntries.GetByLocalDateAsync(targetDate, cancellationToken);

        var projectCache = new Dictionary<Guid, ProjectDto>();
        var customerCache = new Dictionary<Guid, CustomerDto>();

        ActiveTimerSessionDto activeSession = null;
        var status = TimerSessionDtos.Idle;

        if (activeEntry is not null)
        {
            var project = await GetProjectAsync(activeEntry.ProjectId, projectCache, cancellationToken);
            var customer = await GetCustomerAsync(activeEntry.CustomerId, customerCache, cancellationToken);

            var accumulated = _sessionState?.AccumulatedDuration ?? TimeSpan.Zero;
            var nowLocal = GetCurrentLocalTime();
            var currentDuration = CalculateDuration(activeEntry.StartLocal, nowLocal);
            var totalDuration = accumulated + currentDuration;
            var roundedTotalDuration = RoundDuration(totalDuration, allowZeroDuration: false);

            activeSession = new ActiveTimerSessionDto(
                    activeEntry.Id,
                    activeEntry.CustomerId,
                    activeEntry.ProjectId,
                    customer.Name,
                    project.Name,
                    activeEntry.StartLocal,
                    activeEntry.StartUtc,
                    activeEntry.LastModifiedUtc,
                    totalDuration,
                    roundedTotalDuration,
                    false,
                    activeEntry.Notes,
                    activeEntry.Billable,
                    activeEntry.Tag);

            status = TimerSessionDtos.Running;
        }
        else if (_sessionState is not null && _sessionState.IsPaused)
        {
            var project = await GetProjectAsync(_sessionState.ProjectId, projectCache, cancellationToken);
            var customer = await GetCustomerAsync(_sessionState.CustomerId, customerCache, cancellationToken);

            activeSession = new ActiveTimerSessionDto(
                    _sessionState.LastEntryId ?? Guid.Empty,
                    _sessionState.CustomerId,
                    _sessionState.ProjectId,
                    customer.Name,
                    project.Name,
                    _sessionState.LastStartLocal ?? DateTime.MinValue,
                    _sessionState.LastStartUtc ?? DateTime.MinValue,
                    _sessionState.LastInteractionUtc,
                    _sessionState.AccumulatedDuration,
                    _sessionState.RoundedDuration,
                    true,
                    _sessionState.Notes,
                    _sessionState.Billable,
                    _sessionState.Tag);

            status = TimerSessionDtos.Paused;
        }

        var history = new List<TimerHistoryEntryDto>(entries.Count + 1);
        foreach (var entry in entries)
        {
            var historyEntry = await TryCreateHistoryEntryAsync(entry, projectCache, customerCache, cancellationToken);
            if (historyEntry is not null)
            {
                history.Add(historyEntry);
            }
        }

        if (status == TimerSessionDtos.Running && activeEntry is not null)
        {
            var project = await GetProjectAsync(activeEntry.ProjectId, projectCache, cancellationToken);
            var customer = await GetCustomerAsync(activeEntry.CustomerId, customerCache, cancellationToken);
            var nowLocal = GetCurrentLocalTime();
            var runningDuration = CalculateDuration(activeEntry.StartLocal, nowLocal);
            var runningRounded = RoundDuration(runningDuration, allowZeroDuration: true);

            history.Insert(0, new TimerHistoryEntryDto(
                        activeEntry.Id,
                        activeEntry.CustomerId,
                        customer.Name,
                        activeEntry.ProjectId,
                        project.Name,
                        activeEntry.StartLocal,
                        nowLocal,
                        runningDuration,
                        runningRounded,
                        activeEntry.Billable,
                        activeEntry.Notes,
                        activeEntry.Tag));
        }

        return new TimerSessionSnapshotDto(status, activeSession, targetDate, history);
    }

    private async Task<TimeEntryDto> StopEntryInternalAsync(
            TimeEntryDto entry,
            DateTime stopLocal,
            bool isPause,
            CancellationToken cancellationToken,
            TimerSessionStopOptions options = null)
    {
        stopLocal = EnsureLocalKind(stopLocal);
        var entryStartLocal = EnsureLocalKind(entry.StartLocal);

        if (stopLocal < entryStartLocal)
        {
            stopLocal = entryStartLocal;
        }

        var stopUtc = ConvertLocalToUtc(stopLocal);

        var updateDto = new TimeEntryUpdateDto(
                entry.Id,
                null,
                null,
                stopLocal,
                stopUtc,
                options?.Notes,
                options?.Billable,
                options?.Tag,
                null,
                null,
                null);

        var updated = await _timeEntries.UpdateAsync(updateDto, cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException("Failed to update active time entry.");
        }

        if (isPause)
        {
            var entryDuration = CalculateDuration(entry.StartLocal, stopLocal);
            var accumulated = (_sessionState?.AccumulatedDuration ?? TimeSpan.Zero) + entryDuration;
            var roundedAccumulated = RoundDuration(accumulated, allowZeroDuration: true);

            if (_sessionState is null)
            {
                _sessionState = new SessionState(
                        updated.ProjectId,
                        updated.CustomerId,
                        updated.Notes,
                        updated.Billable,
                        updated.Tag ?? string.Empty,
                        accumulated,
                        roundedAccumulated,
                        updated.LastModifiedUtc,
                        updated.Id,
                        updated.StartLocal,
                        updated.StartUtc,
                        stopLocal,
                        stopUtc,
                        true);
            }
            else
            {
                _sessionState = _sessionState with
                {
                    AccumulatedDuration = accumulated,
                    RoundedDuration = roundedAccumulated,
                    Notes = updated.Notes,
                    Billable = updated.Billable,
                    Tag = updated.Tag ?? string.Empty,
                    LastInteractionUtc = updated.LastModifiedUtc,
                    LastEntryId = updated.Id,
                    LastStartLocal = updated.StartLocal,
                    LastStartUtc = updated.StartUtc,
                    LastEndLocal = stopLocal,
                    LastEndUtc = stopUtc,
                    IsPaused = true
                };
            }
        }

        return updated;
    }

    private static TimeSpan CalculateDuration(DateTime startLocal, DateTime stopCandidate)
    {
        if (startLocal.Kind != stopCandidate.Kind)
        {
            startLocal = EnsureUtc(startLocal);
            stopCandidate = EnsureUtc(stopCandidate);
        }

        var duration = stopCandidate - startLocal;
        return duration <= TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static TimeSpan RoundDuration(TimeSpan duration, bool allowZeroDuration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return allowZeroDuration ? TimeSpan.Zero : TimeSpan.FromMinutes(QuarterMinutes);
        }

        var rounded = QuarterHourRounder.Round(duration);
        if (rounded == TimeSpan.Zero && !allowZeroDuration)
        {
            return TimeSpan.FromMinutes(QuarterMinutes);
        }

        return rounded;
    }

    private static void AccumulateDuration(IDictionary<Guid, TimeSpan> durations, Guid projectId, TimeSpan increment)
    {
        if (increment <= TimeSpan.Zero)
        {
            return;
        }

        if (durations.TryGetValue(projectId, out var existing))
        {
            durations[projectId] = existing + increment;
        }
        else
        {
            durations[projectId] = increment;
        }
    }

    private static DateTime EnsureUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime(),
        _ => dateTime
    };

    private DateTime GetCurrentLocalTime()
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _timeZone);
        return EnsureLocalKind(local);
    }

    private DateTime ConvertLocalToUtc(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc)
        {
            return local;
        }

        if (local.Kind == DateTimeKind.Local && _timeZone.Equals(TimeZoneInfo.Local))
        {
            return local.ToUniversalTime();
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    private static DateTime NormalizeLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Local => value,
        _ => DateTime.SpecifyKind(value, DateTimeKind.Local)
    };

    private static DateTime EnsureLocalKind(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => DateTime.SpecifyKind(value.ToLocalTime(), DateTimeKind.Local),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local)
        };
    }

    private async Task<ProjectDto> GetProjectAsync(
            Guid projectId,
            IDictionary<Guid, ProjectDto> cache,
            CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(projectId, out var project))
        {
            project = await _projects.GetByIdAsync(projectId, cancellationToken);
            if (project is null)
            {
                throw new InvalidOperationException($"Project {projectId} not found.");
            }

            cache[projectId] = project;
        }

        return project;
    }

    private async Task<CustomerDto> GetCustomerAsync(
            Guid customerId,
            IDictionary<Guid, CustomerDto> cache,
            CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(customerId, out var customer))
        {
            customer = await _customers.GetByIdAsync(customerId, cancellationToken);
            if (customer is null)
            {
                throw new InvalidOperationException($"Customer {customerId} not found.");
            }

            cache[customerId] = customer;
        }

        return customer;
    }

    private sealed record SessionState(
            Guid ProjectId,
            Guid CustomerId,
            string Notes,
            bool Billable,
            string Tag,
            TimeSpan AccumulatedDuration,
            TimeSpan RoundedDuration,
            DateTime LastInteractionUtc,
            Guid? LastEntryId,
            DateTime? LastStartLocal,
            DateTime? LastStartUtc,
            DateTime? LastEndLocal,
            DateTime? LastEndUtc,
            bool IsPaused);
}
