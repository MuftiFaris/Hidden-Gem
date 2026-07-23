using System.Drawing;
using System.Threading.Tasks;

namespace Assistant.Services
{
    public interface IScreenCaptureService
    {
        /// <summary>Captures the entire primary screen.</summary>
        Task<Bitmap> CaptureFullScreenAsync();

        /// <summary>Captures a specific region of the screen.</summary>
        Task<Bitmap> CaptureRegionAsync(Rectangle region);

        /// <summary>Captures the active window content.</summary>
        Task<Bitmap> CaptureActiveWindowAsync();

        /// <summary>Converts bitmap to base64 for API transmission.</summary>
        string BitmapToBase64(Bitmap bitmap);
    }
}
