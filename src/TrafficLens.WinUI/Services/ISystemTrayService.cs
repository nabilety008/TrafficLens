namespace TrafficLens.WinUI.Services;

public interface ISystemTrayService : IDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? ExitRequested;

    void Show();

    void ShowFirstCloseToTrayNotice();

    void ShowAlert(string title, string message);
}
