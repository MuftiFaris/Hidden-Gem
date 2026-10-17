using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Screen capture service using GDI+ BitBlt for high-performance screenshots.
    /// Supports full screen, window, and region capture.
    /// </summary>
    public sealed class ScreenCaptureService : IScreenCaptureService
    {
        private readonly ILogger<ScreenCaptureService> _logger;

        public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
            => _logger = logger;

        // ── P/Invoke for screen capture ────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowCallback(IntPtr handle, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        public IReadOnlyList<ScreenWindow> GetAvailableWindows()
        {
            var windows = new List<ScreenWindow>();
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out var processId);
                if (!IsWindowVisible(handle) || processId == Environment.ProcessId) return true;
                var title = new StringBuilder(512);
                GetWindowText(handle, title, title.Capacity);
                if (!string.IsNullOrWhiteSpace(title.ToString())) windows.Add(new ScreenWindow(handle, title.ToString()));
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        public Task<Bitmap> CaptureWindowAsync(IntPtr handle)
        {
            return Task.Run(() =>
            {
                if (!IsWindow(handle) || !IsWindowVisible(handle) || IsIconic(handle))
                    throw new InvalidOperationException("Keep the selected window open and visible; it is closed or minimized.");
                if (!GetWindowRect(handle, out var rect)) throw new InvalidOperationException("Cannot read the selected window bounds.");
                var bounds = Rectangle.Intersect(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), SystemInformation.VirtualScreen);
                if (bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidOperationException("The selected window is outside the visible desktop.");
                var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                try
                {
                    using var graphics = Graphics.FromImage(bitmap);
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                    return bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            });
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public Task<Bitmap> CaptureFullScreenAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var screen = Screen.PrimaryScreen;
                    if (screen == null)
                    {
                        _logger.LogError("Primary screen not found");
                        throw new InvalidOperationException("No primary screen available");
                    }
                    
                    var bounds = screen.Bounds;
                    var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                    using var gfx = Graphics.FromImage(bitmap);
                    gfx.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    
                    _logger.LogInformation("Captured full screen: {Width}x{Height}", bounds.Width, bounds.Height);
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture full screen");
                    throw;
                }
            });
        }

        public Task<Bitmap> CaptureRegionAsync(Rectangle region)
        {
            return Task.Run(() =>
            {
                try
                {
                    var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                    using var gfx = Graphics.FromImage(bitmap);
                    gfx.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
                    
                    _logger.LogInformation("Captured region: {X},{Y} {Width}x{Height}", 
                        region.X, region.Y, region.Width, region.Height);
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture region");
                    throw;
                }
            });
        }

        public Task<Bitmap> CaptureCenterScreenAsync(double widthRatio = 0.65, double heightRatio = 0.65)
        {
            return Task.Run(() =>
            {
                try
                {
                    var screen = Screen.PrimaryScreen;
                    if (screen == null)
                    {
                        _logger.LogError("Primary screen not found");
                        throw new InvalidOperationException("No primary screen available");
                    }

                    var bounds = screen.Bounds;
                    int width = Math.Max(100, (int)(bounds.Width * Math.Clamp(widthRatio, 0.2, 1.0)));
                    int height = Math.Max(100, (int)(bounds.Height * Math.Clamp(heightRatio, 0.2, 1.0)));
                    int x = bounds.X + (bounds.Width - width) / 2;
                    int y = bounds.Y + (bounds.Height - height) / 2;

                    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using var gfx = Graphics.FromImage(bitmap);
                    gfx.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

                    _logger.LogInformation("Captured center screen: {X},{Y} {Width}x{Height}", x, y, width, height);
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture center screen");
                    throw;
                }
            });
        }

        public Task<Bitmap> CaptureActiveWindowAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var handle = GetForegroundWindow();
                    if (handle == IntPtr.Zero)
                    {
                        _logger.LogWarning("No foreground window found");
                        return CaptureFullScreenAsync().Result;
                    }

                    if (!GetWindowRect(handle, out var rect))
                    {
                        _logger.LogWarning("Failed to get window rect, falling back to full screen");
                        return CaptureFullScreenAsync().Result;
                    }

                    var width = rect.Right - rect.Left;
                    var height = rect.Bottom - rect.Top;
                    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using var gfx = Graphics.FromImage(bitmap);
                    gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                    
                    _logger.LogInformation("Captured active window: {Width}x{Height}", width, height);
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to capture active window");
                    throw;
                }
            });
        }

        public string BitmapToBase64(Bitmap bitmap)
        {
            try
            {
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Jpeg);
                var bytes = ms.ToArray();
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert bitmap to base64");
                throw;
            }
        }
    }
}
