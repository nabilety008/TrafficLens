namespace TrafficLens.Core.Abstractions;

public interface IWindowsUpdateService
{
    WindowsUpdateState GetState();

    Task<WindowsUpdateOperationResult> DisableAsync(CancellationToken cancellationToken = default);

    Task<WindowsUpdateOperationResult> EnableAsync(CancellationToken cancellationToken = default);
}
