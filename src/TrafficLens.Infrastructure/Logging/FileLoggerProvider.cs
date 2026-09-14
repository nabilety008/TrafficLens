using Microsoft.Extensions.Logging;

namespace TrafficLens.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
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
}