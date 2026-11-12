using Serilog;
using Viewiphone.Forms;
using Viewiphone.Services;

namespace Viewiphone
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            LoggingService.Initialize();

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled AppDomain exception occurred");
            };

            Application.ThreadException += (s, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI ThreadException occurred");
            };

            ApplicationConfiguration.Initialize();
            Log.Information("Starting ViewiPhone Application...");

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal application crash");
                MessageBox.Show($"Application crashed: {ex.Message}", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                LoggingService.CloseAndFlush();
            }
        }
    }
}
