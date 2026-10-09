using System.Globalization;
using Microsoft.Extensions.Logging;

namespace GameTracker.Infrastructure.Logging;

/// <summary>Tiny daily-rolling file logger (logs/gametracker-yyyyMMdd.log, 7 days kept). No external dependency.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 7;

    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly Lock _lock = new();

    public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information)
    {
        _directory = directory;
        _minimumLevel = minimumLevel;
        DeleteOldFiles();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category}: {message}{Environment.NewLine}");
        if (exception is not null)
            line += exception + Environment.NewLine;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(Path.Combine(_directory, $"gametracker-{now:yyyyMMdd}.log"), line);
            }
            catch (IOException)
            {
                // Logging must never break the app.
            }
        }
    }

    private void DeleteOldFiles()
    {
        try
        {
            var limit = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in new DirectoryInfo(_directory).EnumerateFiles("gametracker-*.log"))
            {
                if (file.LastWriteTime < limit)
                    file.Delete();
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        private readonly string _shortCategory = category[(category.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= provider._minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                provider.Write(_shortCategory, logLevel, formatter(state, exception), exception);
        }
    }
}
