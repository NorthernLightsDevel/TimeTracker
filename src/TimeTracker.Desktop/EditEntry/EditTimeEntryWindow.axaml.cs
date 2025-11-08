using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TimeTracker.Desktop.EditEntry;

public partial class EditTimeEntryWindow : Window
{
    public EditTimeEntryWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Close(null);

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EditTimeEntryViewModel vm)
        {
            Close(null);
            return;
        }

        if (vm.TryBuildResult(out var result))
        {
            Close(result);
        }
    }
}
