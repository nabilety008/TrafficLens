using System.ComponentModel;
using System.Diagnostics;

namespace TrafficLens.Network.Process;

/// <summary>
/// Resolves process metadata through <see cref="Process"/>. Every access is
/// individually guarded so protected processes, cross-bitness images and
/// processes that exit between events never throw. Start time is used to
/// detect PID reuse: an existing process whose start time differs from the
/// requested identity by more than <see cref="StartTimeMatchTolerance"/> is
/// reported as a different instance.
/// </summary>
public sealed class WindowsProcessMetadataProvider : IProcessMetadataProvider
{
    private const long StartTimeMatchToleranceTicks = 2 * TimeSpan.TicksPerSecond;

    public ProcessMetadataResult Resolve(ProcessInstanceId identity)
    {
        System.Diagnostics.Process process;
        try
        {
            process = System.Diagnostics.Process.GetProcessById(identity.ProcessId);
        }
        catch (ArgumentException)
        {
            return new ProcessMetadataResult(false, null);
        }
        catch (InvalidOperationException)
        {
            return new ProcessMetadataResult(false, null);
        }
        catch (Win32Exception)
        {
            return new ProcessMetadataResult(false, null);
        }

        using (process)
        {
            long actualStartTicks = 0;
            try
            {
                actualStartTicks = process.StartTime.ToUniversalTime().Ticks;
            }
            catch
            {
                actualStartTicks = 0;
            }

            if (identity.HasStartTime
                && actualStartTicks != 0
                && Math.Abs(actualStartTicks - identity.StartTimeUtcTicks) > StartTimeMatchToleranceTicks)
            {
                return new ProcessMetadataResult(true, null);
            }

            var name = GetProcessName(process);
            var (path, icon) = GetExecutableInfo(process);
            return new ProcessMetadataResult(
                true,
                new ProcessMetadata(name, path, icon, actualStartTicks));
        }
    }

    private static string GetProcessName(System.Diagnostics.Process process)
    {
        try
        {
            var name = process.ProcessName;
            return string.IsNullOrWhiteSpace(name) ? "<unknown>" : name;
        }
        catch
        {
            return "<unknown>";
        }
    }

    private static (string? Path, bool IconAvailable) GetExecutableInfo(System.Diagnostics.Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return (path, path is not null && File.Exists(path));
        }
        catch
        {
            return (null, false);
        }
    }
}