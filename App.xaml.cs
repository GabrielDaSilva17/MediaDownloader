using System;
using System.IO;
using System.Windows;

namespace MediaDownloader
{
    public partial class App : Application
    {
        public static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("App.OnStartup iniciou.");

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Log($"CRASH AppDomain.UnhandledException: {ex}");
                MessageBox.Show(ex?.ToString(), "Erro Fatal AppDomain", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Log($"CRASH DispatcherUnhandledException: {args.Exception}");
                MessageBox.Show(args.Exception.ToString(), "Erro Fatal Dispatcher", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
            Log("App.OnStartup concluído.");
        }
    }
}
