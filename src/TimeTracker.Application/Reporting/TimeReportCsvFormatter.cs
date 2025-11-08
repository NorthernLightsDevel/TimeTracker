using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Reporting;

internal static class TimeReportCsvFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string BuildCsv(IEnumerable<TimerDailySummaryDto> summaries)
    {
        if (summaries is null)
        {
            throw new ArgumentNullException(nameof(summaries));
        }

        var builder = new StringBuilder();
        builder.AppendLine("day,customer,project,totalHours,notes");

        foreach (var summary in summaries.OrderBy(summary => summary.LocalDate))
        {
            var groups = summary.Entries
                .GroupBy(entry => new EntryKey(
                    entry.CustomerId,
                    string.IsNullOrWhiteSpace(entry.CustomerName) ? "Unassigned" : entry.CustomerName,
                    entry.ProjectId,
                    entry.ProjectName ?? "Untitled Project"))
                .OrderBy(group => group.Key.CustomerName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key.ProjectName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var totalHours = group.Sum(entry => entry.Duration.TotalHours);
                var notes = BuildNotes(group);
                AppendRow(builder, summary.LocalDate, group.Key.CustomerName, group.Key.ProjectName, totalHours, notes);
            }
        }

        return builder.ToString();
    }

    private static string BuildNotes(IEnumerable<TimerHistoryEntryDto> entries)
    {
        return string.Join('\n', entries
            .OrderBy(entry => entry.StartLocal)
            .Select(entry =>
            {
                var note = string.IsNullOrWhiteSpace(entry.Notes) ? "(no note)" : entry.Notes.Trim();
                return $"{entry.StartLocal:HH:mm} - {entry.EndLocal:HH:mm}: {note}";
            }));
    }

    private static void AppendRow(StringBuilder builder, DateOnly day, string customer, string project, double totalHours, string notes)
    {
        var fields = new[]
        {
            day.ToString("yyyy-MM-dd", Invariant),
            customer,
            project,
            totalHours.ToString("0.##", Invariant),
            notes
        };

        builder.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var sanitized = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{sanitized}\"" : sanitized;
    }

    private readonly record struct EntryKey(Guid CustomerId, string CustomerName, Guid ProjectId, string ProjectName);
}
