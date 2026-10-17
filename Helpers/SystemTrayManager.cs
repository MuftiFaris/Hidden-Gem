using System;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace Assistant.Helpers
{
    /// <summary>Manages the shared gem tray icon and its show/exit actions.</summary>
    public sealed class SystemTrayManager : IDisposable
    {
        private readonly TaskbarIcon _tray;
        private readonly Window      _owner;
        private bool _disposed;

        public SystemTrayManager(Window owner)
        {
            _owner = owner;
            _tray  = new TaskbarIcon
            {
                ToolTipText = "Hidden Gem",
                Icon        = LoadAppIcon(),
            };

            // Build context menu
            var menu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem { Header = "Show / Hide" };
            showItem.Click += (_, _) => ToggleWindow();

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

            menu.Items.Add(showItem);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(exitItem);

            _tray.ContextMenu = menu;

            // Double-click on tray icon restores the window
            _tray.TrayMouseDoubleClick += (_, _) => ShowWindow();
        }

        public void ShowWindow()
        {
            _owner.Show();
            _owner.WindowState = WindowState.Normal;
            _owner.Activate();
        }

        public void HideToTray() => _owner.Hide();

        private void ToggleWindow()
        {
            if (_owner.IsVisible) HideToTray();
            else ShowWindow();
        }

        private static Icon LoadAppIcon()
        {
            var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            using var stream = resource.Stream;
            using var icon = new Icon(stream, 32, 32);
            return (Icon)icon.Clone();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tray.Icon?.Dispose();
            _tray.Dispose();
            _disposed = true;
        }
    }
}
