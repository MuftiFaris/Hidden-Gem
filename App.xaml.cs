using System;
using System.IO;
using System.Windows;
using Assistant.Services;
using Assistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Assistant
{
    public partial class App : Application
    {
        private ServiceProvider? _services;

        // ── Startup ────────────────────────────────────────────────────────────

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureSerilog();

            var sc = new ServiceCollection();
            RegisterServices(sc);
            _services = sc.BuildServiceProvider();

            // Unhandled-exception safety net
            DispatcherUnhandledException += (_, ex) =>
            {
                var logger = _services.GetService<ILogger<App>>();
                logger?.LogCritical(ex.Exception, "Unhandled dispatcher exception");
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{ex.Exception.Message}",
                    "Hidden Gem",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;

            var settings = _services.GetRequiredService<ISettingsService>().Load();
            if (settings.StartMinimized)
                window.WindowState = WindowState.Minimized;

            window.Show();
        }

        // ── Shutdown ───────────────────────────────────────────────────────────

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            _services?.Dispose();
            base.OnExit(e);
        }

        // ── DI registration ────────────────────────────────────────────────────

        private static void RegisterServices(ServiceCollection sc)
        {
            // Logging
            sc.AddLogging(b => b.AddSerilog(dispose: true));

            // Infrastructure services (singletons — one instance per app lifetime)
            sc.AddSingleton<ICredentialService, CredentialManagerService>();
            sc.AddSingleton<ISettingsService,   SettingsService>();
            sc.AddSingleton<IGeminiService,     GeminiService>();
            sc.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
            sc.AddSingleton<ISpeechService,     SpeechService>();
            sc.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            sc.AddSingleton<IAudioTranscriptionService, AudioTranscriptionService>();
            sc.AddSingleton<IAutoResponseService, AutoResponseService>();

            // View-models (singletons so nav state persists between page switches)
            sc.AddSingleton<ChatViewModel>();
            sc.AddSingleton<SettingsViewModel>();
            sc.AddSingleton<InterviewViewModel>();
            sc.AddSingleton<MainViewModel>();

            // Windows
            sc.AddSingleton<MainWindow>();
            sc.AddTransient<OverlayWindow>();
        }

        // ── Serilog ────────────────────────────────────────────────────────────

        private static void ConfigureSerilog()
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HiddenGem", "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    path:            Path.Combine(logDir, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    // IMPORTANT: conversation content is NOT logged by default.
                    // Only metadata (errors, request counts, settings changes) is written.
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}
