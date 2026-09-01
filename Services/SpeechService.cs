using System;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Speech recognition service using Windows Speech Recognition (System.Speech).
    /// Supports continuous dictation mode for natural language input.
    /// </summary>
    public sealed class SpeechService : ISpeechService, IDisposable
    {
        private readonly ILogger<SpeechService> _logger;
        private SpeechRecognitionEngine? _recognizer;
        private TaskCompletionSource<string>? _recognitionTcs;
        private string _recognizedText = string.Empty;

        public event EventHandler<bool>? IsRecordingChanged;

        public SpeechService(ILogger<SpeechService> logger)
        {
            _logger = logger;
            InitializeRecognizer();
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
            if (_recognizer == null)
            {
                _logger.LogWarning("Speech recognizer not initialized");
                throw new InvalidOperationException("Speech recognition not available");
            }

            _recognitionTcs = new TaskCompletionSource<string>();
            _recognizedText = string.Empty;

            try
            {
                _logger.LogInformation("Starting speech recognition");
                IsRecordingChanged?.Invoke(this, true);
                
                // Use RecognizeAsync with proper timeout handling
                _recognizer.RecognizeAsync(RecognizeMode.Single);

                // Wait for recognition with timeout
                using (ct.Register(() => 
                {
                    _logger.LogInformation("Speech recognition cancelled via token");
                    _recognizer.RecognizeAsyncCancel();
                    _recognitionTcs?.TrySetResult(_recognizedText);
                }))
                {
                    // Add timeout to prevent infinite waiting
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), ct);
                    var resultTask = _recognitionTcs.Task;
                    
                    var completedTask = await Task.WhenAny(resultTask, timeoutTask).ConfigureAwait(false);
                    
                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("Speech recognition timeout after 30 seconds");
                        _recognizer.RecognizeAsyncCancel();
                        return _recognizedText;
                    }
                    
                    var result = await resultTask.ConfigureAwait(false);
                    _logger.LogInformation("Speech recognized: {Length} characters", result.Length);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Speech recognition cancelled");
                _recognizer.RecognizeAsyncCancel();
                return _recognizedText;
            }
            finally
            {
                IsRecordingChanged?.Invoke(this, false);
            }
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
            if (e.Result.Confidence > 0.3f) // Lower threshold from 0.5 to 0.3 for better detection
            {
                _recognizedText = e.Result.Text;
                _logger.LogInformation("Speech recognized with confidence {Confidence}: {Text}", 
                    e.Result.Confidence, e.Result.Text);
            }
            else
            {
                _logger.LogDebug("Low confidence speech rejected: {Confidence} (threshold 0.3)", e.Result.Confidence);
            }
        }

        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            _logger.LogWarning("Speech recognition rejected - no recognizable speech detected");
        }

        private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                _logger.LogError(e.Error, "Speech recognition error");
                _recognitionTcs?.TrySetResult(_recognizedText);
                return;
            }

            if (e.Cancelled)
            {
                _logger.LogInformation("Speech recognition cancelled by system");
                _recognitionTcs?.TrySetResult(_recognizedText);
                return;
            }

            _logger.LogInformation("Speech recognition completed. Result length: {Length}", _recognizedText.Length);
            _recognitionTcs?.TrySetResult(_recognizedText);
        }

        public void Dispose()
        {
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
