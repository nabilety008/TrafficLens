using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace TrafficLens.App.Infrastructure;

internal static class StartupTrace
{
    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("TRAFFICLENS_STARTUP_TRACE"), "1", StringComparison.OrdinalIgnoreCase);
    private static readonly Stopwatch Stopwatch = new();
    private static readonly object Gate = new();
    private static long _sequence = 0;
    private static bool _closed;

    public static void Tick(string stage)
    {
        if (!Enabled)
        {
            return;
        }

        if (!Stopwatch.IsRunning)
        {
            Stopwatch.Start();
        }

        lock (Gate)
        {
            if (_closed)
            {
                return;
            }

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{_sequence},{stage},{Stopwatch.Elapsed.TotalMilliseconds:F1}");

            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "trafficlens-startup-trace.csv"),
                    line + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public static void Close()
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            _closed = true;
        }
    }
}