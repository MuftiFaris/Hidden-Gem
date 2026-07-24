using System;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace Assistant.Helpers
{
    /// <summary>
    /// Manages the system-tray icon and its context-menu actions.
    ///
    /// We create the TaskbarIcon in code so the icon bitmap can be generated
    /// programmatically — eliminating a hard dependency on a bundled .ico file.
    /// If you add Assets/tray.ico to the project (Build Action = Resource) you
    /// can replace CreateFallbackIcon() with:
    ///     Icon = new System.Windows.Media.Imaging.BitmapImage(
    ///                new Uri("pack://application:,,,/Assets/tray.ico"))
    /// </summary>
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
                ToolTipText = "Gemini Assistant",
                Icon        = CreateFallbackIcon(),
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

        /// <summary>
        /// Generates a simple 32×32 "G" icon in memory so the app runs
        /// without requiring a bundled .ico file.
        /// </summary>
        private static Icon CreateFallbackIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g   = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(75, 158, 247));        // accent blue
            using var font  = new Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("G", font, brush, new PointF(5, 3));
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tray.Dispose();
            _disposed = true;
        }
    }
}
