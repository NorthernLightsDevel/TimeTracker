using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeTracker.Application.Reporting;

namespace TimeTracker.Desktop.Reporting;

public partial class DailyReportView : UserControl
{
    public DailyReportView()
    {
        InitializeComponent();
    }

    private async void OnExportWeekClicked(object sender, RoutedEventArgs e)
        => await ExportAsync(TimeReportPreset.Week);

    private async void OnExportMonthClicked(object sender, RoutedEventArgs e)
        => await ExportAsync(TimeReportPreset.Month);

    private async Task ExportAsync(TimeReportPreset preset)
    {
        if (DataContext is not DailyReportViewModel viewModel)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            DefaultExtension = "csv",
            InitialFileName = viewModel.GetSuggestedFileName(preset),
            Filters = new List<FileDialogFilter>
            {
                new()
                {
                    Name = "CSV files",
                    Extensions = { "csv" }
                }
            }
        };

        var targetPath = await dialog.ShowAsync(window).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        try
        {
            var csv = await viewModel.GenerateReportCsvAsync(preset).ConfigureAwait(false);
            await File.WriteAllTextAsync(targetPath, csv).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to export report: {ex.Message}");
        }
    }
}
