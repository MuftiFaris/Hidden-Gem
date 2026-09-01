using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assistant.Services
{
    /// <summary>
    /// Captures system audio (what's playing through speakers) for analysis.
    /// Useful for hearing Zoom, Discord, GMeet participants without audio loopback setup.
    /// </summary>
    public interface IAudioCaptureService : IDisposable
    {
        /// <summary>
        /// Fired when recording starts (true) or stops (false).
        /// </summary>
        event EventHandler<bool>? IsRecordingChanged;

        /// <summary>
        /// Fired when new audio frame is available (16-bit PCM samples).
        /// Provides real-time access to audio for progressive analysis.
        /// </summary>
        event EventHandler<float[]>? AudioFrameReady;

        /// <summary>
        /// Gets available audio devices (render endpoints / loopback sources).
        /// </summary>
        IEnumerable<AudioDevice> GetAvailableDevices();

        /// <summary>
        /// Starts capturing system audio from specified device (or default speaker).
        /// Returns true if successful, false if device not found or permission denied.
        /// </summary>
        Task<bool> StartCapturingAsync(string deviceId = "", CancellationToken ct = default);

        /// <summary>
        /// Stops capturing and returns all accumulated audio samples as float array [-1, 1].
        /// </summary>
        Task<float[]> StopCapturingAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets current audio buffer without stopping recording.
        /// Useful for progressive analysis or silence detection while recording.
        /// </summary>
        float[] GetCurrentBuffer();

        /// <summary>
        /// Detects if current audio buffer contains significant sound.
        /// Returns true if RMS energy > threshold.
        /// 
        /// Typical thresholds:
        ///   - 0.01: Very sensitive (detects typing/breathing)
        ///   - 0.05: Normal (detects speech clearly)
        ///   - 0.10: Less sensitive (detects loud speech only)
        /// </summary>
        bool DetectAudio(float threshold = 0.05f);

        /// <summary>
        /// Clears the audio buffer without stopping recording.
        /// Use before capturing important content to discard silence.
        /// </summary>
        void ClearBuffer();
    }
}
