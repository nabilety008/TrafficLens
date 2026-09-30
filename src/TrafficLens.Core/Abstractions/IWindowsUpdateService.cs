namespace TrafficLens.Core.Abstractions;

public interface IWindowsUpdateService
{
    WindowsUpdateState GetState();

    /// <summary>
    /// Applies the target-feature-update hold for the CURRENT installed Windows
    /// release (security/quality updates keep flowing). One-shot, elevated only
    /// when required, verified after write, reversible.
    /// </summary>
    Task<WindowsUpdateOperationResult> DisableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes only the hold TrafficLens owns, restoring the exact prior state.
    /// </summary>
    Task<WindowsUpdateOperationResult> EnableAsync(CancellationToken cancellationToken = default);
}
