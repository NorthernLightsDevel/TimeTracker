using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using TimeTracker.Desktop.EditEntry;
using TimeTracker.Desktop.ProjectManagement;

namespace TimeTracker.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpenedAsync;
    }

    private async void OnOpenedAsync(object sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private async void OnManageProjectsClicked(object sender, RoutedEventArgs e)
    {
        await ShowProjectManagementDialogAsync();
    }

    private async void OnEditEntryClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedEntry is null)
        {
            return;
        }

        var entry = viewModel.SelectedEntry;
        var dialog = new EditTimeEntryWindow
        {
            DataContext = new EditTimeEntryViewModel(entry.StartLocal, entry.EndLocal, entry.Notes)
        };

        var result = await dialog.ShowDialog<EditTimeEntryResult?>(this);
        if (result.HasValue)
        {
            await viewModel.AdjustEntryAsync(entry.Id, result.Value.StartLocal, result.Value.EndLocal, result.Value.Notes);
        }
    }

    private async Task ShowProjectManagementDialogAsync()
    {
        using var scope = App.CreateScope();
        var projectManagementViewModel = scope.ServiceProvider.GetRequiredService<ProjectManagementViewModel>();

        var dialog = new ProjectManagementWindow
        {
            DataContext = projectManagementViewModel
        };

        var result = await dialog.ShowDialog<bool?>(this);

        if (result.GetValueOrDefault() && DataContext is MainViewModel mainViewModel)
        {
            await mainViewModel.ReloadProjectsAsync();
        }
    }
}
