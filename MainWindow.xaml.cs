using System;
using System.Windows;
using System.Windows.Interop;
using Assistant.Helpers;
using Assistant.Services;
using Assistant.ViewModels;

namespace Assistant
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel     _vm;
        private readonly ISettingsService  _settingsSvc;
        private readonly SystemTrayManager _tray;

        public MainWindow(MainViewModel vm, ISettingsService settingsSvc)
        {
            _vm          = vm;
            _settingsSvc = settingsSvc;
            DataContext  = vm;
            InitializeComponent();

            _tray = new SystemTrayManager(this);
        }

        // ── Window handle available ────────────────────────────────────────────

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // Pass the real HWND to the VM so it can call SetWindowDisplayAffinity
            _vm.WindowHandle = new WindowInteropHelper(this).Handle;
            _vm.ApplyInitialPrivacyMode();
        }

        // ── Title bar drag / resize ────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(
            object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
            => ToggleMaximize();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => HandleClose();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_settingsSvc.Load().MinimizeToTrayOnClose)
            {
                e.Cancel = true;   // suppress window destruction
                _tray.HideToTray();
            }
            else
            {
                _tray.Dispose();
                Application.Current.Shutdown();
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void HandleClose()
        {
            if (_settingsSvc.Load().MinimizeToTrayOnClose)
                _tray.HideToTray();
            else
            {
                _tray.Dispose();
                Application.Current.Shutdown();
            }
        }

        private void ToggleMaximize()
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
    }
}
