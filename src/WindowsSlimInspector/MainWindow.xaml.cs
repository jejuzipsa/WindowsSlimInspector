using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsSlimInspector.Models;
using WindowsSlimInspector.Services;

namespace WindowsSlimInspector;

public partial class MainWindow : Window
{
    private readonly WindowsSlimService _slimService = new();
    private readonly SystemInspectorService _inspector = new();
    private readonly List<FeatureItem> _features;
    private readonly DispatcherTimer _inspectionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _inspectionWatch = new();

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        _features = _slimService.CreateFeatureItems().ToList();
        BuildFeatureRows();
        _inspectionTimer.Tick += (_, _) => InspectionElapsedText.Text = $"{(int)_inspectionWatch.Elapsed.TotalSeconds}초";
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private void EnableDarkTitleBar()
    {
        try { var hwnd = new WindowInteropHelper(this).Handle; var enabled = 1; DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)); }
        catch { }
    }

    private void BuildFeatureRows()
    {
        FeaturePanel.Children.Clear();
        foreach (var feature in _features)
        {
            var grid = new Grid { Margin = new Thickness(2, 5, 2, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center, DataContext = feature }; check.SetBinding(CheckBox.IsCheckedProperty, nameof(FeatureItem.IsSelected)); Grid.SetColumn(check, 0);
            var textStack = new StackPanel { Margin = new Thickness(12, 0, 10, 0) }; textStack.Children.Add(new TextBlock { Text = feature.Name, FontWeight = FontWeights.SemiBold, Foreground = Brushes.WhiteSmoke }); textStack.Children.Add(new TextBlock { Text = feature.Description, Foreground = new SolidColorBrush(Color.FromRgb(170, 182, 199)), TextWrapping = TextWrapping.Wrap }); Grid.SetColumn(textStack, 1);
            var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeights.Bold, DataContext = feature }; status.SetBinding(TextBlock.TextProperty, nameof(FeatureItem.StateText)); status.SetBinding(TextBlock.ForegroundProperty, nameof(FeatureItem.StateBrush)); Grid.SetColumn(status, 2);
            grid.Children.Add(check); grid.Children.Add(textStack); grid.Children.Add(status); FeaturePanel.Children.Add(grid); FeaturePanel.Children.Add(new Separator());
        }
    }

    private async Task RefreshStatusAsync() { ActionStatusText.Text = "상태 확인 중..."; await _slimService.RefreshAsync(_features); UpdateCounters(); ActionStatusText.Text = "현재 Windows 설정을 확인했습니다."; }
    private void UpdateCounters() { TotalCountText.Text = $"{_features.Count}개"; ActiveCountText.Text = $"{_features.Count(x => x.State == FeatureState.Enabled)}개"; InactiveCountText.Text = $"{_features.Count(x => x.State == FeatureState.Disabled)}개"; }
    private void SelectAllChanged(object sender, RoutedEventArgs e) { if (_features is null) return; var value = SelectAllCheckBox.IsChecked == true; foreach (var feature in _features) feature.IsSelected = value; }
    private async void RefreshStatus_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

    private async void DisableSelected_Click(object sender, RoutedEventArgs e)
    {
        var count = _features.Count(x => x.IsSelected); if (count == 0) { ActionStatusText.Text = "선택된 항목이 없습니다."; return; }
        ActionStatusText.Text = $"{count}개 항목 비활성화 적용 중..."; await _slimService.ApplyAsync(_features, true, LogService.AppendSlimLog); await RefreshStatusAsync(); ActionStatusText.Text = $"{count}개 항목 처리 완료 · 일부 변경은 다음 로그인/재부팅 후 완전히 적용될 수 있습니다.";
    }

    private async void RestoreSelected_Click(object sender, RoutedEventArgs e)
    {
        var count = _features.Count(x => x.IsSelected); if (count == 0) { ActionStatusText.Text = "선택된 항목이 없습니다."; return; }
        ActionStatusText.Text = $"{count}개 항목 복구 중..."; await _slimService.ApplyAsync(_features, false, LogService.AppendSlimLog); await RefreshStatusAsync(); ActionStatusText.Text = $"{count}개 항목 복구 처리 완료 · 일부 변경은 다음 로그인/재부팅 후 완전히 적용될 수 있습니다.";
    }

    private async void RunInspection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InspectionButton.IsEnabled = false; InspectionStatusText.Text = "검사 중..."; InspectionProgress.Foreground = new SolidColorBrush(Color.FromRgb(143,47,59)); InspectionProgress.IsIndeterminate = true; InspectionElapsedText.Text = "0초"; _inspectionWatch.Restart(); _inspectionTimer.Start();
            var path = await _inspector.RunAsync(); _inspectionWatch.Stop(); _inspectionTimer.Stop(); InspectionProgress.IsIndeterminate = false; InspectionProgress.Value = 100; InspectionElapsedText.Text = $"{(int)_inspectionWatch.Elapsed.TotalSeconds}초"; InspectionStatusText.Text = $"검사 완료 · {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex) { _inspectionWatch.Stop(); _inspectionTimer.Stop(); InspectionProgress.IsIndeterminate = false; InspectionProgress.Value = 0; InspectionStatusText.Text = "검사 실패"; MessageBox.Show(ex.Message, "WindowsSlimInspector", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { InspectionButton.IsEnabled = true; }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e) { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogService.LogDirectory}\"") { UseShellExecute = true }); }
}
