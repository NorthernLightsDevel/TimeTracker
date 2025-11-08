using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace TimeTracker.Desktop;

public partial class DailyEntryRowView : UserControl
{
    public DailyEntryRowView()
    {
        InitializeComponent();
        Loaded += OnLoadedStatic;
        Unloaded += OnUnloadedStatic;
        DataContextChanged += OnDataContextChangedStatic;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static void OnLoadedStatic(object sender, RoutedEventArgs e)
    {
        if (sender is DailyEntryRowView view)
        {
            view.UpdateVisualState();
        }
    }

    private static void OnUnloadedStatic(object sender, RoutedEventArgs e)
    {
        if (sender is DailyEntryRowView view)
        {
            view.Loaded -= OnLoadedStatic;
            view.Unloaded -= OnUnloadedStatic;
            view.DataContextChanged -= OnDataContextChangedStatic;
        }
    }

    private static void OnDataContextChangedStatic(object sender, EventArgs e)
    {
        if (sender is DailyEntryRowView view)
        {
            view.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (DataContext is DailyEntryItem item)
        {
            PseudoClasses.Set(":running", item.IsRunning);
        }
        else
        {
            PseudoClasses.Set(":running", false);
        }
    }
}
