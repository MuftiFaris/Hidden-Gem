using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Captures system audio (loopback recording) using WASAPI Loopback.
    /// This allows capturing Zoom, Discord, GMeet audio without stereo mix.
    /// 
    /// How it works:
    ///   - Enumerates WASAPI render devices (speakers)
    ///   - Creates loopback recording: device → PCM audio buffer
    ///   - Buffers audio for analysis/transcription
    ///   - Provides real-time sample access for silence detection
    /// </summary>
    public sealed class AudioCaptureService : IAudioCaptureService, IDisposable
    {
        private readonly ILogger<AudioCaptureService> _logger;
        
        private IWaveIn? _waveIn;
        private WaveFileWriter? _waveFileWriter;
        private List<float> _audioBuffer = new();
        private bool _isRecording;

        public event EventHandler<bool>? IsRecordingChanged;
        public event EventHandler<float[]>? AudioFrameReady;  // 16-bit PCM samples normalized to [-1, 1]

        public AudioCaptureService(ILogger<AudioCaptureService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets all available WASAPI render devices (speakers/outputs).
        /// These can be used as loopback recording sources.
        /// </summary>
        public IEnumerable<AudioDevice> GetAvailableDevices()
        {
            return Task.Run(() =>
            {
                var devices = new List<AudioDevice>();

                try
                {
                    // Use WASAPI Loopback to capture what's being played
                    var enumerator = new MMDeviceEnumerator();
                    var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                    foreach (var device in renderDevices)
                    {
                        devices.Add(new AudioDevice
                        {
                            Id = device.ID,
                            Name = device.FriendlyName,
                            IsLoopback = false  // These become loopback sources via WASAPI
                        });
                    }

                    _logger.LogInformation("Found {Count} audio devices available for loopback capture", devices.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enumerate audio devices");
                }

                return devices;
            }).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Starts capturing system audio (e.g., Zoom speaker output) via WASAPI loopback.
        /// Returns samples in real-time via AudioFrameReady event.
        /// </summary>
        public async Task<bool> StartCapturingAsync(string deviceId = "", CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Starting audio capture from device: {Device}", 
                    string.IsNullOrEmpty(deviceId) ? "Default" : deviceId);

                return await Task.Run(() =>
                {
                    var enumerator = new MMDeviceEnumerator();
                    
                    // Get the device to capture from
                    MMDevice? device = null;
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        device = enumerator.GetDevice(deviceId);
                    }
                    else
                    {
                        // Default: capture from default render device (speaker output)
                        device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    }

                    if (device == null)
                    {
                        _logger.LogError("Audio device not found");
                        return false;
                    }

                    // Create loopback recording: what plays through speakers → record it
                    _waveIn = new WasapiLoopbackCapture(device)
                    {
                        ShareMode = AudioClientShareMode.Shared  // Allow multiple apps to capture simultaneously
                    };

                    _waveIn.DataAvailable += OnAudioDataAvailable;
                    _waveIn.RecordingStopped += OnRecordingStopped;

                    _audioBuffer.Clear();
                    _isRecording = true;
                    IsRecordingChanged?.Invoke(this, true);

                    _waveIn.StartRecording();

                    _logger.LogInformation("Audio capture started from: {Device}", device.FriendlyName);
                    return true;
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start audio capture");
                _isRecording = false;
                IsRecordingChanged?.Invoke(this, false);
                return false;
            }
        }

        /// <summary>
        /// Stops capturing audio and returns accumulated samples.
        /// </summary>
        public async Task<float[]> StopCapturingAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Stopping audio capture. Buffer size: {Samples} samples", _audioBuffer.Count);

                if (_waveIn != null)
                {
                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                    _waveIn = null;
                }

                if (_waveFileWriter != null)
                {
                    _waveFileWriter.Dispose();
                    _waveFileWriter = null;
                }

                _isRecording = false;
                IsRecordingChanged?.Invoke(this, false);

                var result = _audioBuffer.ToArray();
                _audioBuffer.Clear();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio capture");
                _isRecording = false;
                IsRecordingChanged?.Invoke(this, false);
                return Array.Empty<float>();
            }
        }

        /// <summary>
        /// Gets accumulated audio buffer without stopping recording.
        /// Useful for progressive analysis while still recording.
        /// </summary>
        public float[] GetCurrentBuffer()
        {
            lock (_audioBuffer)
            {
                return _audioBuffer.ToArray();
            }
        }

        /// <summary>
        /// Detects if audio contains significant sound (above noise floor).
        /// Returns true if audio level is above threshold (detects speech/sound).
        /// </summary>
        public bool DetectAudio(float threshold = 0.05f)
        {
            lock (_audioBuffer)
            {
                if (_audioBuffer.Count == 0) return false;

                // Calculate RMS (root mean square) energy
                var rms = Math.Sqrt(_audioBuffer.Average(x => x * x));
                var detected = rms > threshold;

                if (detected)
                {
                    _logger.LogDebug("Audio detected: RMS={RMS:F4}, Threshold={Threshold:F4}", rms, threshold);
                }

                return detected;
            }
        }

        /// <summary>
        /// Clears the audio buffer without stopping recording.
        /// Use this to discard silence periods before speech.
        /// </summary>
        public void ClearBuffer()
        {
            lock (_audioBuffer)
            {
                _audioBuffer.Clear();
            }
        }

        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // Convert byte buffer to float samples (16-bit PCM)
                var floatSamples = ConvertBytesToFloat(e.Buffer, e.BytesRecorded);

                lock (_audioBuffer)
                {
                    _audioBuffer.AddRange(floatSamples);
                }

                // Fire event for real-time analysis
                if (floatSamples.Length > 0)
                {
                    AudioFrameReady?.Invoke(this, floatSamples);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio data");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                _logger.LogError(e.Exception, "Recording error");
            }
        }

        /// <summary>
        /// Converts 16-bit PCM byte buffer to float samples [-1, 1].
        /// </summary>
        private float[] ConvertBytesToFloat(byte[] buffer, int bytesRecorded)
        {
            var floatBuffer = new float[bytesRecorded / 2];

            for (int i = 0; i < floatBuffer.Length; i++)
            {
                // Read 16-bit signed int (little-endian)
                short sample = BitConverter.ToInt16(buffer, i * 2);
                // Normalize to [-1, 1]
                floatBuffer[i] = sample / 32768f;
            }

            return floatBuffer;
        }

        public void Dispose()
        {
            _waveIn?.Dispose();
            _waveFileWriter?.Dispose();
        }
    }

    /// <summary>
    /// Audio device descriptor for UI selection.
    /// </summary>
    public class AudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsLoopback { get; set; }

        public override string ToString() => Name;
    }
}
