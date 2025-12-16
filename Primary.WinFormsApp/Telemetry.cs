using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Primary.WinFormsApp
{
    /// <summary>
    /// Local-only telemetry shim. Keeps the same API surface but avoids sending data to external services.
    /// </summary>
    internal class Telemetry
    {
        public static bool AppInsightsEnabled { get; private set; }

        public static readonly string AppName = Assembly.GetExecutingAssembly().GetName().Name;
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static readonly string SessionId = Guid.NewGuid().ToString();

        /// <summary>
        /// Initializes the telemetry subsystem. Only local console output is performed.
        /// </summary>
        public static void InitializeLogging()
        {
            AppInsightsEnabled = false;
            LogLocal("Telemetry initialized (local only, no external collectors).", null, LogLevel.Information);
        }

        /// <summary>
        /// Shuts down telemetry. No-op for local logging.
        /// </summary>
        public static void ShutdownLogging()
        {
            // No external resources to dispose.
        }

        private class TelemetryTimeTracker : IDisposable
        {
            private readonly Stopwatch _stopwatch;
            private readonly string _message;

            public TelemetryTimeTracker(string message)
            {
                _stopwatch = Stopwatch.StartNew();
                _message = message;
                LogInformation("Start " + message);
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                LogInformation("Completed " + _message + " time elapsed: " + _stopwatch.ElapsedMilliseconds);
            }
        }

        public static IDisposable TrackTime(string msg)
        {
            return new TelemetryTimeTracker(msg);
        }

        public static void LogInformation(string msg)
        {
            Log(msg, logLevel: LogLevel.Information);
        }

        public static void LogWarning(string msg, Exception ex = null)
        {
            Log(msg, ex, false, LogLevel.Warning);
        }

        public static void LogError(string msg, Exception ex = null, bool unhandledException = true)
        {
            Log(msg, ex, unhandledException, LogLevel.Error);
        }

        /// <summary>
        /// Logs a message locally (console). No external telemetry is used.
        /// </summary>
        public static void Log(string msg, Exception ex = null, bool unhandledException = false, LogLevel logLevel = LogLevel.Error)
        {
            var timestamp = DateTimeOffset.Now.ToString("u", CultureInfo.InvariantCulture);
            var levelTag = ((int)logLevel) + " - " + logLevel.ToString();
            var header = $"[{timestamp}] [{levelTag}]";

            if (ex != null)
            {
                var errorMessage = $"{header} {msg} :: {ex.GetType().Name} - {ex.GetBaseException().Message}";
                LogLocal(errorMessage, ex, logLevel);
            }
            else
            {
                LogLocal($"{header} {msg}", null, logLevel);
            }
        }

        public static void LogLocal(string message, Exception ex, LogLevel logLevel = LogLevel.Error)
        {
            if (ex != null)
            {
                Console.Error.WriteLine($"{message}{Environment.NewLine}{ex}");
            }
            else
            {
                if (logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
                    Console.Error.WriteLine(message);
                else
                    Console.WriteLine(message);
            }
        }
    }

    public enum LogLevel
    {
        Error,
        Information,
        Warning,
        Debug
    }
}
