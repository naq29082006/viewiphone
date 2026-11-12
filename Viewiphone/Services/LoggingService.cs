using Serilog;

namespace Viewiphone.Services
{
    public static class LoggingService
    {
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "viewiphone-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _initialized = true;
            Log.Information("LoggingService initialized. Log directory: {LogDir}", logDirectory);
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
