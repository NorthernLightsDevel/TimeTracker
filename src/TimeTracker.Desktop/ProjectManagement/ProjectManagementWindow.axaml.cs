using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TimeTracker.Desktop.ProjectManagement;

public partial class ProjectManagementWindow : Window
{
    public ProjectManagementWindow()
    {
        InitializeComponent();
        Opened += OnOpenedAsync;
    }

    private async void OnOpenedAsync(object sender, EventArgs e)
    {
        if (DataContext is ProjectManagementViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        var result = (DataContext as ProjectManagementViewModel)?.HasChanges ?? false;
        Close(result);
    }
}
