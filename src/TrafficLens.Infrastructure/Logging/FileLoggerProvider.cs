using System.Globalization;
using Microsoft.Extensions.Logging;

namespace TrafficLens.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 14;

    private readonly string _directory;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        PruneExpiredLogs();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(_directory, $"trafficlens-{date}.log");
        return new FileLogger(filePath, categoryName, _ => null);
    }

    public void Dispose()
    {
    }

    private void PruneExpiredLogs()
    {
        try
        {
            var cutoff = DateTime.Now.Date.AddDays(-RetentionDays);
            foreach (var filePath in Directory.EnumerateFiles(_directory, "trafficlens-*.log"))
            {
                var datePart = Path.GetFileNameWithoutExtension(filePath)["trafficlens-".Length..];
                if (DateTime.TryParseExact(
                    datePart,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date) && date < cutoff)
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}