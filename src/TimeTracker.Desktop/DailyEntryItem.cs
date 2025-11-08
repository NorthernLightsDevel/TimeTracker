using System;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Desktop;

public sealed class DailyEntryItem
{
    public Guid Id { get; }
    public Guid ProjectId { get; }
    public string ProjectName { get; }
    public DateTime StartLocal { get; }
    public DateTime EndLocal { get; }
    public TimeSpan Duration { get; }
    public TimeSpan RoundedDuration { get; }
    public bool Billable { get; }
    public string Notes { get; }
    public string Tag { get; }
    public bool IsRunning { get; }

    public string StartDisplay => StartLocal.ToString("HH:mm");
    public string EndDisplay => IsRunning ? "Running" : EndLocal.ToString("HH:mm");
    public string DurationDisplay => Format(Duration);
    public string RoundedDurationDisplay => Format(RoundedDuration);
    public string NotesDisplay => Notes;
    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public bool CanDelete => !IsRunning;

    private DailyEntryItem(
        Guid id,
        Guid projectId,
        string projectName,
        DateTime startLocal,
        DateTime endLocal,
        TimeSpan duration,
        TimeSpan roundedDuration,
        bool billable,
        string notes,
        string tag,
        bool isRunning)
    {
        Id = id;
        ProjectId = projectId;
        ProjectName = projectName;
        StartLocal = startLocal;
        EndLocal = endLocal;
        Duration = duration;
        RoundedDuration = roundedDuration;
        Billable = billable;
        Notes = notes ?? string.Empty;
        Tag = tag ?? string.Empty;
        IsRunning = isRunning;
    }

    public static DailyEntryItem FromHistoryEntry(TimerHistoryEntryDto entry, bool isRunning = false)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        var tag = entry.Tag ?? string.Empty;

        return new DailyEntryItem(
            entry.TimeEntryId,
            entry.ProjectId,
            entry.ProjectName,
            entry.StartLocal,
            entry.EndLocal,
            entry.Duration,
            entry.RoundedDuration,
            entry.Billable,
            entry.Notes,
            tag,
            isRunning);
    }

    private static string Format(TimeSpan value) => value.ToString(@"hh\:mm");
}
