using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Assistant.Helpers
{
    /// <summary>
    /// Wraps the SetWindowDisplayAffinity Windows API to implement "privacy mode".
    ///
    /// HOW IT WORKS
    /// ────────────
    /// SetWindowDisplayAffinity instructs the DWM (Desktop Window Manager) to limit
    /// where a window's pixels can appear.  The relevant values are:
    ///
    ///   WDA_NONE (0x0000)              — default; no restriction
    ///   WDA_MONITOR (0x0001)           — GDI-based capture (BitBlt, PrintWindow) sees
    ///                                    a black rectangle instead of the window content
    ///   WDA_EXCLUDEFROMCAPTURE (0x0011)— Added in Windows 10 2004 (build 19041).
    ///                                    Also excludes the window from the Windows
    ///                                    Graphics Capture API (WGC) used by Snipping Tool,
    ///                                    Xbox Game Bar, Microsoft Teams share, etc.
    ///
    /// KNOWN LIMITATIONS (always disclose to users)
    /// ────────────────────────────────────────────
    ///   ✗  OBS Studio in "Window Capture" mode uses its own injection path and may
    ///      still capture the content depending on the capture method chosen.
    ///   ✗  Hardware-level recording (GPU overlays, HDMI capture cards) is unaffected.
    ///   ✗  RDP / TeamViewer / remote desktop software operate at a different layer
    ///      and are generally not restricted.
    ///   ✗  Older or non-DWM-aware screen recorders using raw GDI may behave differently.
    ///   ✗  WDA_EXCLUDEFROMCAPTURE requires Windows 10 version 2004+; on older builds
    ///      the call falls back to WDA_MONITOR which provides partial protection.
    ///
    /// API documentation: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
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
