using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TimeTracker.Application.Services;
using TimeTracker.Desktop.Infrastructure;
using TimeTracker.Application.Reporting;

namespace TimeTracker.Desktop.Reporting;

public sealed class DailyReportViewModel : INotifyPropertyChanged
{
    private const int DefaultDayWindow = 7;

    private readonly ITimerSessionService _timerService;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DailyReportViewModel> _logger;
    private readonly ITimeReportExporter _reportExporter;
    private readonly RelayCommand _refreshCommand;
    private DateOnly _anchorDate;

    private bool _isBusy;
    private bool _hasResults;
    private string _lastUpdatedDisplay = "Not refreshed yet";

    public DailyReportViewModel(
        ITimerSessionService timerService,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider,
        ITimeReportExporter reportExporter,
        ILogger<DailyReportViewModel> logger)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _anchorDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        _refreshCommand = new RelayCommand(_ => _ = RefreshAsync(_anchorDate), _ => !IsBusy);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<DailyReportGroupViewModel> Groups { get; } = new();

    public ICommand RefreshCommand => _refreshCommand;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasResults
    {
        get => _hasResults;
        private set
        {
            if (SetProperty(ref _hasResults, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool HasNoResults => !HasResults;

    public string LastUpdatedDisplay
    {
        get => _lastUpdatedDisplay;
        private set => SetProperty(ref _lastUpdatedDisplay, value);
    }

    public Task RefreshAsync(DateOnly anchorDate, CancellationToken cancellationToken = default)
    {
        _anchorDate = anchorDate;
        return RefreshInternalAsync(anchorDate, cancellationToken);
    }

    public Task<string> GenerateReportCsvAsync(TimeReportPreset preset, CancellationToken cancellationToken = default)
        => _reportExporter.BuildCsvAsync(preset, cancellationToken);

    public string GetSuggestedFileName(TimeReportPreset preset)
    {
        var suffix = preset == TimeReportPreset.Week ? "week" : "month";
        var nowLocal = _timeProvider.GetLocalNow().DateTime;
        return $"timetracker-report-{suffix}-{nowLocal:yyyyMMdd}.csv";
    }

    private async Task RefreshInternalAsync(DateOnly anchorDate, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        await SetBusyAsync(true).ConfigureAwait(false);

        try
        {
            var nowLocal = _timeProvider.GetLocalNow().DateTime;
            var startDate = anchorDate.AddDays(-(DefaultDayWindow - 1));

            var summaries = await _timerService
                .GetDailySummaryAsync(startDate, anchorDate, cancellationToken)
                .ConfigureAwait(false);

            var groups = summaries
                .OrderByDescending(summary => summary.LocalDate)
                .Select(summary => DailyReportGroupViewModel.FromSummary(summary, anchorDate))
                .ToList();

            await RunOnUiThreadAsync(() =>
            {
                Groups.Clear();
                foreach (var group in groups)
                {
                    Groups.Add(group);
                }

                HasResults = Groups.Count > 0;
                LastUpdatedDisplay = nowLocal.ToString("MMM d, yyyy HH:mm");
            }).ConfigureAwait(false);

            _logger.LogInformation("Daily report refreshed with {GroupCount} group(s).", groups.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh daily timer report.");
        }
        finally
        {
            await SetBusyAsync(false).ConfigureAwait(false);
        }
    }

    private Task SetBusyAsync(bool value)
    {
        if (_dispatcher.CheckAccess())
        {
            IsBusy = value;
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(() => IsBusy = value);
    }

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
}
