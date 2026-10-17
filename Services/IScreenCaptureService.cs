using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace Assistant.Services
{
    public sealed record ScreenWindow(IntPtr Handle, string Title);

    public interface IScreenCaptureService
    {
        IReadOnlyList<ScreenWindow> GetAvailableWindows();
        Task<Bitmap> CaptureWindowAsync(IntPtr handle);

        /// <summary>Captures the entire primary screen.</summary>
        Task<Bitmap> CaptureFullScreenAsync();

        /// <summary>Captures a specific region of the screen.</summary>
        Task<Bitmap> CaptureRegionAsync(Rectangle region);

        /// <summary>Captures the center portion of the primary screen (useful for focusing on interview question modals/windows).</summary>
        Task<Bitmap> CaptureCenterScreenAsync(double widthRatio = 0.65, double heightRatio = 0.65);

        /// <summary>Captures the active window content.</summary>
        Task<Bitmap> CaptureActiveWindowAsync();

        /// <summary>Converts bitmap to base64 for API transmission.</summary>
        string BitmapToBase64(Bitmap bitmap);
    }
}
