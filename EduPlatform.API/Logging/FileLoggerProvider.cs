using Microsoft.Extensions.Logging;

namespace EduPlatform.API.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly object _syncRoot = new();

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logDirectory, _syncRoot);
    }

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly object _syncRoot;

        public FileLogger(string categoryName, string logDirectory, object syncRoot)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
            _syncRoot = syncRoot;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var fileName = $"eduplatform-api-{DateTime.UtcNow:yyyy-MM-dd}.log";
            var path = Path.Combine(_logDirectory, fileName);
            var message = formatter(state, exception);
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} {logLevel}] {_categoryName}: {message}";

            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (_syncRoot)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
