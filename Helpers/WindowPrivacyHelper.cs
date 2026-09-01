using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Assistant.Helpers
{
    /// <summary>
    /// Wraps SetWindowDisplayAffinity Windows API for "privacy mode".
    /// Blocks Windows Graphics Capture API (Snipping Tool, Xbox Game Bar, Teams).
    /// Does NOT block: Zoom, OBS, hardware recorders, RDP, TeamViewer.
    /// Requires Windows 10 2004+.
    /// </summary>
    public static class WindowPrivacyHelper
    {
        private const uint WDA_NONE               = 0x00000000;
        private const uint WDA_MONITOR            = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint dwAffinity);

        /// <summary>
        /// Enables or disables privacy mode for the specified window.
        /// Returns true if the OS accepted the affinity value.
        /// </summary>
        public static bool SetPrivacyMode(IntPtr hwnd, bool enable, ILogger? logger = null)
        {
            if (hwnd == IntPtr.Zero)
            {
                logger?.LogWarning("SetPrivacyMode called with zero HWND — skipped");
                return false;
            }

            if (enable)
            {
                // Try the stronger mode first (Windows 10 2004+)
                if (SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE))
                {
                    logger?.LogInformation("Privacy mode: WDA_EXCLUDEFROMCAPTURE applied");
                    return true;
                }

                int err = Marshal.GetLastWin32Error();
                logger?.LogWarning(
                    "WDA_EXCLUDEFROMCAPTURE failed (Win32={Error}). " +
                    "Falling back to WDA_MONITOR — this requires Windows 10 2004+.", err);

                // Fallback to WDA_MONITOR (supported on older Win10 builds)
                if (SetWindowDisplayAffinity(hwnd, WDA_MONITOR))
                {
                    logger?.LogInformation("Privacy mode: WDA_MONITOR applied (partial protection)");
                    return true;
                }

                err = Marshal.GetLastWin32Error();
                logger?.LogError(
                    "WDA_MONITOR also failed (Win32={Error}). Privacy mode is NOT active.", err);
                return false;
            }
            else
            {
                bool ok = SetWindowDisplayAffinity(hwnd, WDA_NONE);
                if (ok)
                    logger?.LogInformation("Privacy mode: removed (WDA_NONE)");
                else
                    logger?.LogWarning("Failed to clear display affinity (Win32={Error})",
                        Marshal.GetLastWin32Error());
                return ok;
            }
        }

        /// <summary>Returns true when any non-zero affinity is active on the window.</summary>
        public static bool IsPrivacyModeActive(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            GetWindowDisplayAffinity(hwnd, out uint affinity);
            return affinity != WDA_NONE;
        }
    }
}
