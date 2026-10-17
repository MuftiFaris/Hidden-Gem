using System;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Speech recognition service using NAudio microphone + Gemini transcription.
    /// Falls back to Windows Speech Recognition if NAudio unavailable.
    /// 
    /// Strategy:
    ///   1. Try NAudio microphone capture (more reliable)
    ///   2. Record audio from mic
    ///   3. Send to Gemini for transcription
    ///   4. Return transcribed text
    ///   
    /// Fallback: Windows Speech Recognition (if NAudio fails)
    /// </summary>
    public sealed class SpeechService : ISpeechService, IDisposable
    {
        private readonly ILogger<SpeechService> _logger;
        private readonly IGeminiService? _gemini;  // Optional for transcription
        private readonly ISettingsService? _settings;
        private readonly ICredentialService? _creds;  // Optional for API key
        private SpeechRecognitionEngine? _recognizer;  // Fallback
        private IWaveIn? _waveIn;  // NAudio microphone
        private TaskCompletionSource<string>? _recordingTcs;

        public event EventHandler<bool>? IsRecordingChanged;

        public SpeechService(
            ILogger<SpeechService> logger,
            IGeminiService? gemini = null,
            ICredentialService? creds = null, ISettingsService? settings = null)
        {
            _logger = logger;
            _gemini = gemini;
            _creds = creds;
            _settings = settings;
            InitializeMicrophone();
        }

        public bool IsAvailable()
        {
            try
            {
                var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
                return WaveIn.DeviceCount > 0 || recognizers.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check speech recognition availability");
                return false;
            }
        }

        public string[] GetAvailableDevices()
        {
            // System.Speech uses default audio input device
            // For advanced device selection, would need NAudio or similar
            return new[] { "Default Microphone" };
        }

        public async Task<string> RecognizeSpeechAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Starting speech recognition (NAudio microphone)");
                IsRecordingChanged?.Invoke(this, true);

                var connection = _settings?.Load() ?? new AppSettings();
                // Try NAudio microphone first
                if (_waveIn != null)
                {
                    var audioData = await RecordFromMicrophoneAsync(ct).ConfigureAwait(false);
                    
                    if (audioData.Length > 0 && _gemini != null && _creds != null)
                    {
                        // Transcribe using Gemini
                        var apiKey = _creds.GetApiKey(connection.Provider);
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            var text = await TranscribeAudioAsync(audioData, apiKey, connection, ct).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(text))
                            {
                                _logger.LogInformation("Speech transcribed: {Length} chars", text.Length);
                                return text;
                            }
                        }
                    }
                }

                // Fallback to Windows Speech Recognition if NAudio fails
                _logger.LogWarning("Falling back to Windows Speech Recognition");
                return await RecognizeSpeechWindowsAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                IsRecordingChanged?.Invoke(this, false);
            }
        }

        private async Task<byte[]> RecordFromMicrophoneAsync(CancellationToken ct)
        {
            using var capture = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
            using var buffer = new System.IO.MemoryStream();
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var heardSpeech = false;
            var silenceMs = 0;
            capture.DataAvailable += (_, e) =>
            {
                buffer.Write(e.Buffer, 0, e.BytesRecorded);
                double energy = 0;
                for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
                {
                    var sample = BitConverter.ToInt16(e.Buffer, i) / 32768.0;
                    energy += sample * sample;
                }
                var rms = Math.Sqrt(energy / Math.Max(1, e.BytesRecorded / 2));
                if (rms > 0.012) { heardSpeech = true; silenceMs = 0; }
                else if (heardSpeech) silenceMs += e.BytesRecorded * 1000 / capture.WaveFormat.AverageBytesPerSecond;
                if (silenceMs >= 1200) finished.TrySetResult(true);
            };
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) { stopped.TrySetException(e.Exception); finished.TrySetException(e.Exception); }
                else stopped.TrySetResult(true);
            };
            capture.StartRecording();
            try
            {
                await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromSeconds(30), ct)).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (finished.Task.IsCompleted) await finished.Task.ConfigureAwait(false);
            }
            finally
            {
                capture.StopRecording();
                await stopped.Task.ConfigureAwait(false);
            }
            if (!heardSpeech) return Array.Empty<byte>();
            using var wav = new System.IO.MemoryStream();
            using (var writer = new WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(wav), capture.WaveFormat))
            {
                var pcm = buffer.ToArray();
                writer.Write(pcm, 0, pcm.Length);
            }
            return wav.ToArray();
        }

        private async Task<string> TranscribeAudioAsync(byte[] audioData, string apiKey, AppSettings connection, CancellationToken ct)
        {
            var result = await _gemini!.SendAudioMessageAsync(audioData, apiKey,
                new AppSettings { Provider = connection.Provider, RouterBaseUrl = connection.RouterBaseUrl, RouterTranscriptionModel = connection.RouterTranscriptionModel, RequestTimeoutSeconds = connection.RequestTimeoutSeconds, SelectedModel = connection.SelectedModel, Temperature = 0, MaxOutputTokens = 2048 }, ct).ConfigureAwait(false);
            return result.Trim() == "[INAUDIBLE]" ? string.Empty : result.Trim();
        }

        private async Task<string> RecognizeSpeechWindowsAsync(CancellationToken ct)
        {
            if (_recognizer == null)
            {
                _logger.LogWarning("Windows Speech Recognizer not available");
                return string.Empty;
            }

            _recordingTcs = new TaskCompletionSource<string>();

            try
            {
                _logger.LogInformation("Using Windows Speech Recognition fallback");
                _recognizer.RecognizeAsync(RecognizeMode.Single);

                using (ct.Register(() =>
                {
                    _recognizer.RecognizeAsyncCancel();
                    _recordingTcs?.TrySetResult(string.Empty);
                }))
                {
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), ct);
                    var resultTask = _recordingTcs.Task;

                    var completed = await Task.WhenAny(resultTask, timeoutTask).ConfigureAwait(false);
                    if (completed == timeoutTask)
                    {
                        _recognizer.RecognizeAsyncCancel();
                        return string.Empty;
                    }

                    return await resultTask.ConfigureAwait(false);
                }
            }
            finally
            {
                IsRecordingChanged?.Invoke(this, false);
            }
        }

        private void InitializeMicrophone()
        {
            try
            {
                // Try to initialize NAudio microphone
                _waveIn = new WaveInEvent();
                _logger.LogInformation("NAudio microphone initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize NAudio microphone, will use Windows Speech Recognition");
                _waveIn = null;
            }

            // Also try to initialize Windows Speech Recognition as fallback
            InitializeRecognizer();
        }

        private void InitializeRecognizer()
        {
            try
            {
                // Try to get recognizer for current culture
                var culture = CultureInfo.CurrentCulture;
                var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
                
                if (recognizers.Count == 0)
                {
                    _logger.LogError("No speech recognizers installed on this system");
                    return;
                }

                var recognizer = recognizers.FirstOrDefault(r => r.Culture.Equals(culture));
                if (recognizer == null)
                {
                    // Fallback to en-US if available
                    recognizer = recognizers.FirstOrDefault(r => r.Culture.Name == "en-US");
                }

                if (recognizer == null)
                {
                    _logger.LogWarning("No speech recognizer found for culture {Culture}", culture.Name);
                    _logger.LogWarning("Available recognizers: {Recognizers}", 
                        string.Join(", ", recognizers.Select(r => r.Culture.Name)));
                    return;
                }

                _recognizer = new SpeechRecognitionEngine(recognizer.Id);
                
                _recognizer.LoadGrammar(new DictationGrammar());
                
                // Try to set input device - verify microphone is available
                try
                {
                    _recognizer.SetInputToDefaultAudioDevice();
                    _logger.LogInformation("Microphone input device set successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set microphone input device - check microphone permissions");
                    return;
                }
                
                // Adjust timeouts for better detection
                _recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1.0);     // Shorter end silence
                _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(10);   // Longer initial wait
                _recognizer.BabbleTimeout = TimeSpan.FromSeconds(2);            // Shorter babble timeout
                
                // Lower confidence threshold for better detection
                _recognizer.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 30);  // From 60 to 30
                _recognizer.UpdateRecognizerSetting("AdaptationOn", 1);

                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                _recognizer.RecognizeCompleted += OnRecognizeCompleted;

                _logger.LogInformation("Speech recognizer initialized successfully: {Culture} (threshold 30%, timeout 10s)", 
                    recognizer.Culture.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize speech recognizer");
            }
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            _logger.LogInformation("Speech recognized (confidence {Confidence}): {Text}", 
                e.Result.Confidence, e.Result.Text);
        }

        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            _logger.LogWarning("Speech recognition rejected - no recognizable speech");
        }

        private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                _logger.LogError(e.Error, "Speech recognition error");
            }
            _recordingTcs?.TrySetResult(e.Result?.Text ?? string.Empty);
        }

        public void Dispose()
        {
            _waveIn?.Dispose();

            if (_recognizer != null)
            {
                _recognizer.SpeechRecognized -= OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected -= OnSpeechRejected;
                _recognizer.RecognizeCompleted -= OnRecognizeCompleted;
                _recognizer.Dispose();
                _recognizer = null;
            }
        }
    }
}
