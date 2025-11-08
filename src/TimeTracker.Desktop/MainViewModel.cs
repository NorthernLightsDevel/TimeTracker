using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Services;
using TimeTracker.Desktop.Infrastructure;
using TimeTracker.Desktop.Reporting;
using TimeTracker.Domain.Dtos;
using TimeTracker.Domain.Utilities;

namespace TimeTracker.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const string DefaultStatus = "Ready";

    private readonly ICustomerRepository _customerRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITimerSessionService _timerService;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DailyReportViewModel _reportViewModel;

    private readonly ObservableCollection<ProjectSelectionItem> _projects = new();
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _pauseCommand;
    private readonly RelayCommand _resumeCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _previousDayCommand;
    private readonly RelayCommand _nextDayCommand;
    private readonly RelayCommand _deleteEntryCommand;

    private ProjectSelectionItem _selectedProject;
    private DailyEntryItem _selectedEntry;
    private string _notes = string.Empty;
    private string _statusMessage = DefaultStatus;
    private string _elapsedDisplay = "00:00:00";
    private bool _isBusy;
    private bool _hasProjects;
    private TimerSessionDtos _sessionStatus = TimerSessionDtos.Idle;
    private ActiveTimerSessionDto _activeSession;
    private DispatcherTimer _elapsedTimer;
    private DateTime _snapshotLocalTimestamp;
    private TimeSpan _elapsedAtSnapshot;
    private bool _isInitialized;
    private DateOnly _selectedDate;
    private string _selectedDateDisplay = DateTime.Now.ToString("MMMM d, yyyy");
    private string _selectedDayTotalDurationDisplay = "00:00";
    private string _selectedDayTotalRoundedDurationDisplay = "00:00";
    private bool _hasEntries;
    private bool _canNavigateForward;

    public MainViewModel(
        ICustomerRepository customerRepository,
        IProjectRepository projectRepository,
        ITimerSessionService timerService,
        IUiDispatcher dispatcher,
        DailyReportViewModel reportViewModel,
        TimeProvider timeProvider,
        ILogger<MainViewModel> logger)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _reportViewModel = reportViewModel ?? throw new ArgumentNullException(nameof(reportViewModel));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ReportViewModel = _reportViewModel;

        ExitCommand = new RelayCommand(_ => OnQuit());

        _startCommand = new RelayCommand(_ => _ = StartAsync(), _ => CanStart());
        _pauseCommand = new RelayCommand(_ => _ = PauseAsync(), _ => CanPause());
        _resumeCommand = new RelayCommand(_ => _ = ResumeAsync(), _ => CanResume());
        _stopCommand = new RelayCommand(_ => _ = StopAsync(), _ => CanStop());
        _previousDayCommand = new RelayCommand(_ => _ = GoToPreviousDayAsync(), _ => CanNavigateBackward());
        _nextDayCommand = new RelayCommand(_ => _ = GoToNextDayAsync(), _ => CanNavigateForward);
        _deleteEntryCommand = new RelayCommand(
            parameter => _ = DeleteEntryAsync(parameter as DailyEntryItem),
            parameter => CanDeleteEntry(parameter as DailyEntryItem));

        StartCommand = _startCommand;
        PauseCommand = _pauseCommand;
        ResumeCommand = _resumeCommand;
        StopCommand = _stopCommand;
        PreviousDayCommand = _previousDayCommand;
        NextDayCommand = _nextDayCommand;
        DeleteEntryCommand = _deleteEntryCommand;

        UpdateSelectedDate(DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<ProjectSelectionItem> Projects => _projects;

    public DailyReportViewModel ReportViewModel { get; }

    public ObservableCollection<DailyEntryItem> TodaysEntries { get; } = new();

    public ICommand ExitCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand DeleteEntryCommand { get; }

    public ProjectSelectionItem SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public DailyEntryItem SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(CanEditSelectedEntry));
                OnPropertyChanged(nameof(CanDeleteSelectedEntry));
                _deleteEntryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ElapsedDisplay
    {
        get => _elapsedDisplay;
        private set => SetProperty(ref _elapsedDisplay, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                UpdateCommandStates();
                OnPropertyChanged(nameof(CanEditSelectedEntry));
                OnPropertyChanged(nameof(CanDeleteSelectedEntry));
                _deleteEntryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasProjects
    {
        get => _hasProjects;
        private set
        {
            if (SetProperty(ref _hasProjects, value))
            {
                OnPropertyChanged(nameof(HasNoProjects));
                UpdateCommandStates();
            }
        }
    }

    public bool HasNoProjects => !HasProjects;

    public TimerSessionDtos SessionStatus
    {
        get => _sessionStatus;
        private set
        {
            if (SetProperty(ref _sessionStatus, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsIdle));
                UpdateCommandStates();
            }
        }
    }

    public bool IsRunning => SessionStatus == TimerSessionDtos.Running;
    public bool IsPaused => SessionStatus == TimerSessionDtos.Paused;
    public bool IsIdle => SessionStatus == TimerSessionDtos.Idle;

    public bool CanEditSelectedEntry => !_isBusy && SelectedEntry != null && !SelectedEntry.IsRunning;
    public bool CanDeleteSelectedEntry => !_isBusy && SelectedEntry != null && !SelectedEntry.IsRunning;

    public string SelectedDateDisplay
    {
        get => _selectedDateDisplay;
        private set => SetProperty(ref _selectedDateDisplay, value);
    }

    public string SelectedDayTotalDurationDisplay
    {
        get => _selectedDayTotalDurationDisplay;
        private set => SetProperty(ref _selectedDayTotalDurationDisplay, value);
    }

    public string SelectedDayTotalRoundedDurationDisplay
    {
        get => _selectedDayTotalRoundedDurationDisplay;
        private set => SetProperty(ref _selectedDayTotalRoundedDurationDisplay, value);
    }

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(HasNoEntries));
            }
        }
    }

    public bool HasNoEntries => !HasEntries;

    public bool CanNavigateForward
    {
        get => _canNavigateForward;
        private set
        {
            if (SetProperty(ref _canNavigateForward, value))
            {
                _nextDayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        try
        {
            _logger.LogInformation("Initializing main view model.");
            await LoadProjectsAsync(cancellationToken).ConfigureAwait(false);
            await RefreshSnapshotAsync(DefaultStatus, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main view model.");
            StatusMessage = "Failed to load data.";
        }
    }

    public void Dispose()
    {
        if (_elapsedTimer is not null)
        {
            _elapsedTimer.Tick -= OnElapsedTick;
            _elapsedTimer.Stop();
            _elapsedTimer = null;
        }
    }

    public Task ReloadProjectsAsync(CancellationToken cancellationToken = default)
        => LoadProjectsAsync(cancellationToken);

    private async Task LoadProjectsAsync(CancellationToken cancellationToken)
    {
        var previousSelection = SelectedProject?.ProjectId;
        var items = new List<ProjectSelectionItem>();

        var customers = await _customerRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var customer in customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var customerProjects = await _projectRepository
                .GetByCustomerAsync(customer.Id, includeInactive: false, cancellationToken)
                .ConfigureAwait(false);

            foreach (var project in customerProjects.Where(p => p.IsActive).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(new ProjectSelectionItem(project.Id, customer.Id, project.Name, customer.Name));
            }
        }

        await RunOnUiThreadAsync(() =>
        {
            _projects.Clear();
            foreach (var item in items)
            {
                _projects.Add(item);
            }

            HasProjects = _projects.Count > 0;

            if (HasProjects)
            {
                var matching = previousSelection.HasValue
                    ? _projects.FirstOrDefault(p => p.ProjectId == previousSelection.Value)
                    : null;

                if (matching is not null)
                {
                    SelectedProject = matching;
                }
                else if (SelectedProject is null && _projects.Count > 0)
                {
                    SelectedProject = _projects[0];
                }
            }
            else
            {
                SelectedProject = null;
            }
        }).ConfigureAwait(false);
    }

    private async Task RefreshSnapshotAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _timerService.GetSnapshotAsync(_selectedDate, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Snapshot returned {EntryCount} entries (status {Status}).", snapshot.Entries.Count, snapshot.Status);
            await ApplySnapshotAsync(snapshot, message).ConfigureAwait(false);
            await _reportViewModel.RefreshAsync(_selectedDate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh timer snapshot.");
            StatusMessage = "Unable to load timer state.";
        }
    }

    private async Task StartAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Select a project before starting.";
            return;
        }

        await ExecuteTimerCommandAsync(() =>
        {
            var options = new TimerSessionStartOptions(
                SelectedProject.ProjectId,
                SelectedProject.CustomerId,
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                Billable: true);

            return _timerService.StartAsync(options);
        });
    }

    private Task PauseAsync() => ExecuteTimerCommandAsync(() => _timerService.PauseAsync());

    private Task ResumeAsync() => ExecuteTimerCommandAsync(() => _timerService.ResumeAsync());

    private Task StopAsync() => ExecuteTimerCommandAsync(() =>
        _timerService.StopAsync(new TimerSessionStopOptions(
            string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim())));

    public Task AdjustEntryAsync(Guid entryId, DateTime newStartLocal, DateTime newEndLocal, string notes)
        => ExecuteTimerCommandAsync(() =>
            _timerService.AdjustEntryAsync(
                new TimeEntryAdjustmentOptions(entryId, newStartLocal, newEndLocal, notes)));

    private Task DeleteEntryAsync(DailyEntryItem entry)
    {
        var target = entry ?? SelectedEntry;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        return ExecuteTimerCommandAsync(() => _timerService.DeleteEntryAsync(target.Id));
    }

    private bool CanDeleteEntry(DailyEntryItem entry)
    {
        var target = entry ?? SelectedEntry;
        return !_isBusy && target != null && !target.IsRunning;
    }

    private Task GoToPreviousDayAsync() => NavigateToAsync(_selectedDate.AddDays(-1));

    private Task GoToNextDayAsync()
    {
        if (!CanNavigateForward)
        {
            return Task.CompletedTask;
        }

        return NavigateToAsync(_selectedDate.AddDays(1));
    }

    private async Task NavigateToAsync(DateOnly targetDate)
    {
        if (IsBusy)
        {
            return;
        }

        await SetBusyAsync(true).ConfigureAwait(false);

        try
        {
            UpdateSelectedDate(targetDate);
            await RefreshSnapshotAsync($"Loaded {SelectedDateDisplay} entries", CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await SetBusyAsync(false).ConfigureAwait(false);
        }
    }

    private bool CanNavigateBackward() => true;

    private async Task ExecuteTimerCommandAsync(Func<Task<TimerCommandResultDto>> command)
    {
        if (command is null)
        {
            return;
        }

        if (IsBusy)
        {
            return;
        }

        await SetBusyAsync(true).ConfigureAwait(false);

        try
        {
            var result = await command().ConfigureAwait(false);
            await ApplySnapshotAsync(result.Snapshot, result.Message).ConfigureAwait(false);
            await _reportViewModel.RefreshAsync(_selectedDate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Timer command failed.");
            StatusMessage = "Timer command failed.";
        }
        finally
        {
            await SetBusyAsync(false).ConfigureAwait(false);
        }
    }

    private Task ApplySnapshotAsync(TimerSessionSnapshotDto snapshot, string message)
    {
        return RunOnUiThreadAsync(() =>
        {
            SessionStatus = snapshot.Status;
            _activeSession = snapshot.ActiveSession;

            _snapshotLocalTimestamp = _timeProvider.GetLocalNow().DateTime;
            _elapsedAtSnapshot = _activeSession?.AccumulatedDuration ?? TimeSpan.Zero;

            var totalDuration = TimeSpan.Zero;
            var totalRounded = TimeSpan.Zero;
            var perProjectDurations = new Dictionary<Guid, TimeSpan>();

            var previouslySelectedEntryId = SelectedEntry?.Id;

            TodaysEntries.Clear();
            var runningId = _activeSession != null ? _activeSession.TimeEntryId : Guid.Empty;

            foreach (var entry in snapshot.Entries.OrderByDescending(e => e.StartLocal))
            {
                totalDuration += entry.Duration;
                if (entry.Duration > TimeSpan.Zero)
                {
                    if (perProjectDurations.TryGetValue(entry.ProjectId, out var existing))
                    {
                        perProjectDurations[entry.ProjectId] = existing + entry.Duration;
                    }
                    else
                    {
                        perProjectDurations[entry.ProjectId] = entry.Duration;
                    }
                }

                var isRunning = runningId != Guid.Empty && entry.TimeEntryId == runningId;
                TodaysEntries.Add(DailyEntryItem.FromHistoryEntry(entry, isRunning));
            }

            foreach (var duration in perProjectDurations.Values)
            {
                if (duration <= TimeSpan.Zero)
                {
                    continue;
                }

                totalRounded += QuarterHourRounder.Round(duration);
            }

            _logger.LogInformation("Populated {Count} entries for today.", TodaysEntries.Count);

            HasEntries = TodaysEntries.Count > 0;

            if (previouslySelectedEntryId.HasValue)
            {
                var match = TodaysEntries.FirstOrDefault(e => e.Id == previouslySelectedEntryId.Value);
                SelectedEntry = match;
            }
            else
            {
                SelectedEntry = null;
            }

            SelectedDateDisplay = FormatDate(snapshot.LocalDate);
            SelectedDayTotalDurationDisplay = FormatDuration(totalDuration);
            SelectedDayTotalRoundedDurationDisplay = FormatDuration(totalRounded);

            if (_activeSession is not null)
            {
                var matchingProject = _projects.FirstOrDefault(p => p.ProjectId == _activeSession.ProjectId);
                if (matchingProject is not null)
                {
                    SelectedProject = matchingProject;
                }

                if (!string.Equals(Notes, _activeSession.Notes, StringComparison.Ordinal))
                {
                    Notes = _activeSession.Notes;
                }

                if (SessionStatus == TimerSessionDtos.Running)
                {
                    StartElapsedTimer();
                }
                else
                {
                    StopElapsedTimer();
                }
            }
            else
            {
                StopElapsedTimer();
                if (SessionStatus == TimerSessionDtos.Idle)
                {
                    Notes = string.Empty;
                }
            }

            UpdateElapsedDisplay();
            StatusMessage = message;

            HasProjects = _projects.Count > 0;
        });
    }

    private void UpdateSelectedDate(DateOnly date)
    {
        _selectedDate = date;
        SelectedDateDisplay = FormatDate(date);
        UpdateNavigationState();
    }

    private void UpdateNavigationState()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        CanNavigateForward = _selectedDate < today;
        _previousDayCommand.RaiseCanExecuteChanged();
    }

    private bool CanStart() => !_isBusy && (IsIdle || IsPaused) && SelectedProject is not null && HasProjects;
    private bool CanPause() => !_isBusy && IsRunning;
    private bool CanResume() => !_isBusy && IsPaused;
    private bool CanStop() => !_isBusy && IsRunning;

    private void UpdateCommandStates()
    {
        _startCommand.RaiseCanExecuteChanged();
        _pauseCommand.RaiseCanExecuteChanged();
        _resumeCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _deleteEntryCommand.RaiseCanExecuteChanged();
    }

    private void StartElapsedTimer()
    {
        if (_elapsedTimer is null)
        {
            _elapsedTimer = _dispatcher.CreateTimer(TimeSpan.FromSeconds(1), OnElapsedTick);
        }

        if (!_elapsedTimer.IsEnabled)
        {
            _elapsedTimer.Start();
        }
    }

    private void StopElapsedTimer()
    {
        if (_elapsedTimer is not null && _elapsedTimer.IsEnabled)
        {
            _elapsedTimer.Stop();
        }
    }

    private void OnElapsedTick(object sender, EventArgs e) => UpdateElapsedDisplay();

    private void UpdateElapsedDisplay()
    {
        var elapsed = _elapsedAtSnapshot;

        if (_activeSession is not null && SessionStatus == TimerSessionDtos.Running)
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            if (now > _snapshotLocalTimestamp)
            {
                elapsed += now - _snapshotLocalTimestamp;
            }
        }

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        ElapsedDisplay = elapsed.ToString(@"hh\:mm\:ss");
    }

    private void OnQuit()
    {
        _logger.LogInformation("Exit command invoked by user.");

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private Task SetBusyAsync(bool value)
        => RunOnUiThreadAsync(() => IsBusy = value);

    private Task RunOnUiThreadAsync(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(action);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatDate(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue).ToString("MMMM d, yyyy");

    private static string FormatDuration(TimeSpan value) => value.ToString(@"hh\:mm");
}

public sealed record class ProjectSelectionItem(
    Guid ProjectId,
    Guid CustomerId,
    string ProjectName,
    string CustomerName)
{
    public string DisplayName => string.IsNullOrWhiteSpace(CustomerName)
        ? ProjectName
        : $"{CustomerName} — {ProjectName}";
}
