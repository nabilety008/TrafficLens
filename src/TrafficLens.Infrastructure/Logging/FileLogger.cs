using Microsoft.Extensions.Logging;

namespace TrafficLens.Infrastructure.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _categoryName;
    private readonly Func<string, object?> _errorDetailsFactory;
    private readonly object _lock = new();

    public FileLogger(string filePath, string categoryName, Func<string, object?> errorDetailsFactory)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _errorDetailsFactory = errorDetailsFactory;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Trace;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var entry = new
        {
            timestamp = DateTimeOffset.Now,
            level = logLevel.ToString(),
            category = _categoryName,
            eventId = eventId.Id,
            message,
            exception = exception?.ToString()
        };

        var line = System.Text.Json.JsonSerializer.Serialize(entry);

        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging must never crash the application.
            }
        }
    }

    public string GetErrorDetails(string message)
    {
        var error = _errorDetailsFactory(message);
        return error?.ToString() ?? message;
    }
}