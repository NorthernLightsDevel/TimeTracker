using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Cli;

internal static class StatusFormatter
{
    private const string NoDurationPlaceholder = "--:--";

    public static string FormatPlain(TimerSessionSnapshotDto snapshot)
    {
        if (snapshot is null)
        {
            return "Unknown";
        }

        return snapshot.Status switch
        {
            TimerSessionDtos.Running => FormatActive("Running", snapshot.ActiveSession),
            TimerSessionDtos.Paused => FormatActive("Paused", snapshot.ActiveSession),
            _ => FormatIdle(snapshot)
        };
    }

    public static StatusPayload CreatePayload(TimerSessionSnapshotDto snapshot)
    {
        if (snapshot is null)
        {
            return new StatusPayload("Unknown", null, Array.Empty<HistoryPayload>());
        }

        StatusActivePayload active = null;
        if (snapshot.ActiveSession is not null)
        {
            var activeSession = snapshot.ActiveSession;
            active = new StatusActivePayload(
                activeSession.TimeEntryId,
                activeSession.CustomerId,
                activeSession.ProjectId,
                activeSession.CustomerName,
                activeSession.ProjectName,
                activeSession.StartLocal,
                activeSession.StartUtc,
                activeSession.LastInteractionUtc,
                activeSession.AccumulatedDuration,
                activeSession.RoundedDuration,
                activeSession.IsPaused,
                activeSession.Notes,
                activeSession.Billable,
                activeSession.Tag);
        }

        var history = snapshot.Entries
            .Select(entry => new HistoryPayload(
                entry.TimeEntryId,
                entry.ProjectId,
                entry.ProjectName,
                entry.StartLocal,
                entry.EndLocal,
                entry.Duration,
                entry.RoundedDuration,
                entry.Billable,
                entry.Notes,
                entry.Tag))
            .ToArray();

        return new StatusPayload(snapshot.Status.ToString(), active, history)
        {
            LocalDate = snapshot.LocalDate
        };
    }

    public static WaybarPayload CreateWaybarPayload(TimerSessionSnapshotDto snapshot)
    {
        var status = snapshot?.Status.ToString() ?? "Unknown";
        if (string.IsNullOrWhiteSpace(status))
        {
            status = "Unknown";
        }

        var active = snapshot?.ActiveSession;
        var project = active?.ProjectName ?? string.Empty;
        var customer = active?.CustomerName ?? string.Empty;
        var notes = active?.Notes ?? string.Empty;
        var elapsed = active is null ? NoDurationPlaceholder : FormatDuration(active.AccumulatedDuration);
        var projectAndCustomer = FormatProjectAndCustomer(project, customer);

        var text = status switch
        {
            nameof(TimerSessionDtos.Running) when string.IsNullOrWhiteSpace(projectAndCustomer) => elapsed,
            nameof(TimerSessionDtos.Running) => $"{elapsed} {projectAndCustomer}".Trim(),
            nameof(TimerSessionDtos.Paused) when string.IsNullOrWhiteSpace(projectAndCustomer) => "Paused",
            nameof(TimerSessionDtos.Paused) => $"Paused {projectAndCustomer}".Trim(),
            nameof(TimerSessionDtos.Idle) => "Idle",
            _ => status
        };

        var icon = status switch
        {
            nameof(TimerSessionDtos.Running) => "",
            nameof(TimerSessionDtos.Paused) => "",
            nameof(TimerSessionDtos.Idle) => "",
            _ => ""
        };

        var tooltip = BuildTooltip(status, project, customer, elapsed, notes, snapshot?.Entries);
        var cssClass = $"timetracker-{status.ToLowerInvariant()}";
        var alt = status.ToLowerInvariant();

        return new WaybarPayload(text, status, project, customer, elapsed, notes, icon, cssClass, alt, tooltip);
    }

    private static string FormatIdle(TimerSessionSnapshotDto snapshot)
    {
        if (snapshot.Entries.Count == 0)
        {
            return "Idle";
        }

        var recent = snapshot.Entries[0];
        var duration = FormatDuration(recent.Duration);
        return $"Idle (last: {recent.ProjectName} {duration})";
    }

    private static string FormatActive(string prefix, ActiveTimerSessionDto active)
    {
        if (active is null)
        {
            return prefix;
        }

        var duration = FormatDuration(active.AccumulatedDuration);
        var noteSegment = string.IsNullOrWhiteSpace(active.Notes) ? string.Empty : $" - {active.Notes}";
        return $"{prefix} {duration} {active.ProjectName}{noteSegment}".Trim();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalMinutes = (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return $"{hours:00}:{minutes:00}";
    }

    private static string BuildTooltip(string status, string project, string customer, string elapsed, string notes, IReadOnlyList<TimerHistoryEntryDto> entries)
    {
        var lines = new List<string> { $"Status: {status}" };

        if (!string.IsNullOrWhiteSpace(project))
        {
            lines.Add($"Project: {project}");
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            lines.Add($"Customer: {customer}");
        }

        if (!string.IsNullOrWhiteSpace(elapsed) && elapsed != NoDurationPlaceholder)
        {
            lines.Add($"Elapsed: {elapsed}");
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            lines.Add($"Notes: {notes}");
        }

        if (entries is { Count: > 0 })
        {
            var last = entries[0];
            var durationSource = last.RoundedDuration != TimeSpan.Zero || last.Duration == TimeSpan.Zero
                ? last.RoundedDuration
                : last.Duration;

            var shortened = FormatDuration(durationSource);
            lines.Add($"Last entry: {last.ProjectName} {shortened}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatProjectAndCustomer(string project, string customer)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(customer))
        {
            return project;
        }

        return $"{project} · {customer}";
    }

    public sealed record class StatusPayload(
        string Status,
        StatusActivePayload Active,
        IReadOnlyList<HistoryPayload> Entries)
    {
        public DateOnly LocalDate { get; init; }
    }

    public sealed record class StatusActivePayload(
        Guid TimeEntryId,
        Guid CustomerId,
        Guid ProjectId,
        string CustomerName,
        string ProjectName,
        DateTime StartLocal,
        DateTime StartUtc,
        DateTime LastInteractionUtc,
        TimeSpan AccumulatedDuration,
        TimeSpan RoundedDuration,
        bool IsPaused,
        string Notes,
        bool Billable,
        string Tag);

    public sealed record class HistoryPayload(
        Guid TimeEntryId,
        Guid ProjectId,
        string ProjectName,
        DateTime StartLocal,
        DateTime EndLocal,
        TimeSpan Duration,
        TimeSpan RoundedDuration,
        bool Billable,
        string Notes,
        string Tag);

    public sealed record class WaybarPayload(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("project")] string Project,
        [property: JsonPropertyName("customer")] string Customer,
        [property: JsonPropertyName("elapsed")] string Elapsed,
        [property: JsonPropertyName("notes")] string Notes,
        [property: JsonPropertyName("icon")] string Icon,
        [property: JsonPropertyName("class")] string Class,
        [property: JsonPropertyName("alt")] string Alt,
        [property: JsonPropertyName("tooltip")] string Tooltip);
}
