using System;
using System.Collections.Generic;
using System.Linq;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Desktop.Reporting;

public sealed class DailyReportGroupViewModel
{
    public DailyReportGroupViewModel(
        DateOnly localDate,
        TimeSpan totalDuration,
        TimeSpan totalRoundedDuration,
        IReadOnlyList<DailyEntryItem> entries,
        bool isToday)
    {
        LocalDate = localDate;
        TotalDuration = totalDuration;
        TotalRoundedDuration = totalRoundedDuration;
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        IsToday = isToday;
    }

    public DateOnly LocalDate { get; }
    public TimeSpan TotalDuration { get; }
    public TimeSpan TotalRoundedDuration { get; }
    public IReadOnlyList<DailyEntryItem> Entries { get; }
    public bool IsToday { get; }

    public string DateDisplay => LocalDate.ToDateTime(TimeOnly.MinValue).ToString("MMMM d, yyyy");
    public string DayOfWeekDisplay => LocalDate.ToDateTime(TimeOnly.MinValue).ToString("dddd");
    public string TotalDurationDisplay => Format(TotalDuration);
    public string TotalRoundedDurationDisplay => Format(TotalRoundedDuration);
    public int EntryCount => Entries.Count;

    public static DailyReportGroupViewModel FromSummary(
        TimerDailySummaryDto summary,
        DateOnly today)
    {
        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        var entryVms = summary.Entries
            .OrderByDescending(entry => entry.StartLocal)
            .Select(entry => DailyEntryItem.FromHistoryEntry(entry))
            .ToList();

        return new DailyReportGroupViewModel(
            summary.LocalDate,
            summary.TotalDuration,
            summary.TotalRoundedDuration,
            entryVms,
            summary.LocalDate == today);
    }

    private static string Format(TimeSpan value) => value.ToString(@"hh\:mm");
}
