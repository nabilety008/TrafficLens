namespace TrafficLens.App.ViewModels;

public sealed class AlertRuleViewModel : ViewModelBase
{
    private Action? _onEdited;
    private bool _isEnabled;
    private string _thresholdText = string.Empty;
    private int _unitIndex;

    public AlertRuleViewModel(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string Name { get; set; } = string.Empty;

    public string[] UnitOptions { get; set; } = Array.Empty<string>();

    public void AttachOnEdited(Action onEdited) => _onEdited = onEdited;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                _onEdited?.Invoke();
            }
        }
    }

    public string ThresholdText
    {
        get => _thresholdText;
        set
        {
            if (SetProperty(ref _thresholdText, value))
            {
                _onEdited?.Invoke();
            }
        }
    }

    public int UnitIndex
    {
        get => _unitIndex;
        set
        {
            if (SetProperty(ref _unitIndex, value))
            {
                _onEdited?.Invoke();
            }
        }
    }
}