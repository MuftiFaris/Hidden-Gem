using System;
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
