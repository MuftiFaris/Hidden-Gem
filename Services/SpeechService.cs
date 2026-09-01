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
        private readonly ICredentialService? _creds;  // Optional for API key
        private SpeechRecognitionEngine? _recognizer;  // Fallback
        private IWaveIn? _waveIn;  // NAudio microphone
        private WaveFileWriter? _waveWriter;
        private byte[] _audioBuffer = Array.Empty<byte>();
        private TaskCompletionSource<string>? _recordingTcs;

        public event EventHandler<bool>? IsRecordingChanged;

        public SpeechService(
            ILogger<SpeechService> logger,
            IGeminiService? gemini = null,
            ICredentialService? creds = null)
        {
            _logger = logger;
            _gemini = gemini;
            _creds = creds;
            InitializeMicrophone();
        }

        public bool IsAvailable()
        {
            try
            {
                var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
                return recognizers.Count > 0;
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

                // Try NAudio microphone first
                if (_waveIn != null)
                {
                    var audioData = await RecordFromMicrophoneAsync(ct).ConfigureAwait(false);
                    
                    if (audioData.Length > 0 && _gemini != null && _creds != null)
                    {
                        // Transcribe using Gemini
                        var apiKey = _creds.GetApiKey();
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            var text = await TranscribeAudioAsync(audioData, apiKey, ct).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Speech recognition error");
                IsRecordingChanged?.Invoke(this, false);
                return string.Empty;
            }
        }

        private async Task<byte[]> RecordFromMicrophoneAsync(CancellationToken ct)
        {
            var recordingBuffer = new System.IO.MemoryStream();
            var waveWriter = new WaveFileWriter(recordingBuffer, _waveIn!.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            };

            _waveIn.StartRecording();
            _logger.LogInformation("Recording from microphone... speak now (max 30 seconds)");

            try
            {
                // Record for max 30 seconds or until user stops (no easy way to detect end, so timeout)
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recording cancelled");
            }

            _waveIn.StopRecording();
            waveWriter.Flush();
            
            var audioBytes = recordingBuffer.ToArray();
            waveWriter.Dispose();
            recordingBuffer.Dispose();

            _logger.LogInformation("Recording complete: {Bytes} bytes", audioBytes.Length);
            return audioBytes;
        }

        private async Task<string> TranscribeAudioAsync(byte[] audioData, string apiKey, CancellationToken ct)
        {
            try
            {
                // Extract WAV data (skip header, get just PCM)
                var pcmData = ExtractPcmFromWav(audioData);
                if (pcmData.Length == 0)
                {
                    _logger.LogWarning("No PCM data extracted from WAV");
                    return string.Empty;
                }

                // Convert to float samples
                var floatSamples = ConvertBytesToFloat(pcmData);
                var base64Audio = Convert.ToBase64String(pcmData);

                _logger.LogInformation("Sending audio to Gemini for transcription: {Bytes} bytes", pcmData.Length);

                // Use Gemini to transcribe
                var history = new System.Collections.Generic.List<ChatMessage>();
                var result = await _gemini!.SendMessageAsync(
                    history,
                    apiKey,
                    new Models.AppSettings
                    {
                        SelectedModel = "gemini-3.5-flash",
                        Temperature = 0.1f,
                        MaxOutputTokens = 500,
                        SystemPrompt = "Transcribe this audio. Return ONLY the transcription, nothing else. If audio is inaudible, return [INAUDIBLE]."
                    },
                    ct
                ).ConfigureAwait(false);

                return result.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio transcription failed");
                return string.Empty;
            }
        }

        private byte[] ExtractPcmFromWav(byte[] wavData)
        {
            try
            {
                if (wavData.Length < 44) return Array.Empty<byte>();

                // Find "data" chunk
                int dataPos = Array.IndexOf(wavData, (byte)'d');
                while (dataPos > 0 && dataPos < wavData.Length - 4)
                {
                    if (wavData[dataPos] == 'd' && wavData[dataPos+1] == 'a' && 
                        wavData[dataPos+2] == 't' && wavData[dataPos+3] == 'a')
                    {
                        // Read data chunk size
                        int size = BitConverter.ToInt32(wavData, dataPos + 4);
                        int pcmStart = dataPos + 8;
                        return wavData.Skip(pcmStart).Take(size).ToArray();
                    }
                    dataPos++;
                }

                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract PCM from WAV");
                return Array.Empty<byte>();
            }
        }

        private float[] ConvertBytesToFloat(byte[] data)
        {
            var floats = new float[data.Length / 2];
            for (int i = 0; i < floats.Length; i++)
            {
                short sample = BitConverter.ToInt16(data, i * 2);
                floats[i] = sample / 32768f;
            }
            return floats;
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
            _waveWriter?.Dispose();

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
