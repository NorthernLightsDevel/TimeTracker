using System;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Application.Services;

namespace TimeTracker.Application.Reporting;

public sealed class TimeReportExporter : ITimeReportExporter
{
    private readonly ITimerSessionService _timerService;
    private readonly TimeProvider _timeProvider;

    public TimeReportExporter(ITimerSessionService timerService, TimeProvider timeProvider = null)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<string> BuildCsvAsync(TimeReportPreset preset, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var (start, end) = preset switch
        {
            TimeReportPreset.Week => (today.AddDays(-6), today),
            TimeReportPreset.Month => (today.AddDays(-29), today),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown report preset.")
        };

        return BuildCsvAsync(start, end, cancellationToken);
    }

    public async Task<string> BuildCsvAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be greater than or equal to the start date.", nameof(endDate));
        }

        var summaries = await _timerService
            .GetDailySummaryAsync(startDate, endDate, cancellationToken)
            .ConfigureAwait(false);

        return TimeReportCsvFormatter.BuildCsv(summaries);
    }
}
