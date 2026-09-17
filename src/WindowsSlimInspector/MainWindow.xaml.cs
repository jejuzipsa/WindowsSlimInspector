using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WindowsSlimInspector.Models;
using WindowsSlimInspector.Services;

namespace WindowsSlimInspector;

public partial class MainWindow : Window
{
    private readonly WindowsSlimService _slimService = new();
    private readonly SystemInspectorService _inspector = new();
    private readonly List<FeatureItem> _features;

    public MainWindow()
    {
        InitializeComponent();
        _features = _slimService.CreateFeatureItems().ToList();
        BuildFeatureRows();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private void BuildFeatureRows()
    {
        FeaturePanel.Children.Clear();
        foreach (var feature in _features)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

            var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center, DataContext = feature };
            check.SetBinding(CheckBox.IsCheckedProperty, nameof(FeatureItem.IsSelected));
            Grid.SetColumn(check, 0);

            var textStack = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
            textStack.Children.Add(new TextBlock { Text = feature.Name, FontWeight = FontWeights.SemiBold });
            textStack.Children.Add(new TextBlock { Text = feature.Description, Foreground = System.Windows.Media.Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(textStack, 1);

            var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeights.SemiBold, DataContext = feature };
            status.SetBinding(TextBlock.TextProperty, nameof(FeatureItem.StateText));
            status.SetBinding(TextBlock.ForegroundProperty, nameof(FeatureItem.StateBrush));
            Grid.SetColumn(status, 2);

            grid.Children.Add(check);
            grid.Children.Add(textStack);
            grid.Children.Add(status);
            FeaturePanel.Children.Add(grid);
            FeaturePanel.Children.Add(new Separator());
        }
    }

    private async Task RefreshStatusAsync()
    {
        await _slimService.RefreshAsync(_features);
    }

    private void SelectAllChanged(object sender, RoutedEventArgs e)
    {
        if (_features is null) return;
        var value = SelectAllCheckBox.IsChecked == true;
        foreach (var feature in _features) feature.IsSelected = value;
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

    private async void DisableSelected_Click(object sender, RoutedEventArgs e)
    {
        await _slimService.ApplyAsync(_features, disable: true, LogService.AppendSlimLog);
    }

    private async void RestoreSelected_Click(object sender, RoutedEventArgs e)
    {
        await _slimService.ApplyAsync(_features, disable: false, LogService.AppendSlimLog);
    }

    private async void RunInspection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InspectionStatusText.Text = "검사 중...";
            var path = await _inspector.RunAsync();
            InspectionStatusText.Text = $"완료: {System.IO.Path.GetFileName(path)}";
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            InspectionStatusText.Text = "검사 실패";
            MessageBox.Show(ex.Message, "WindowsSlimInspector", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
