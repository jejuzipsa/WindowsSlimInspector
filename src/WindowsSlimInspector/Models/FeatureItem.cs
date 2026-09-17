using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WindowsSlimInspector.Models;

public enum FeatureState
{
    Unknown,
    Enabled,
    Disabled,
    Unsupported,
    Error
}

public sealed class FeatureItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private FeatureState _state = FeatureState.Unknown;
    private string _detail = string.Empty;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public FeatureState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(StateBrush));
        }
    }

    public string Detail
    {
        get => _detail;
        set { _detail = value; OnPropertyChanged(); }
    }

    public string StateText => State switch
    {
        FeatureState.Enabled => "활성",
        FeatureState.Disabled => "비활성",
        FeatureState.Unsupported => "지원 안 됨",
        FeatureState.Error => "확인 실패",
        _ => "확인 중"
    };

    public Brush StateBrush => State switch
    {
        FeatureState.Disabled => Brushes.IndianRed,
        FeatureState.Enabled => Brushes.LimeGreen,
        FeatureState.Unsupported => Brushes.DarkGray,
        FeatureState.Error => Brushes.DarkOrange,
        _ => Brushes.SlateGray
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
