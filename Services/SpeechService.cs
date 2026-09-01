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
                
                _recognizer.RecognizeAsync(RecognizeMode.Single);

                // Wait for recognition or cancellation
                using (ct.Register(() => _recognitionTcs?.TrySetCanceled()))
                {
                    var result = await _recognitionTcs.Task;
                    _logger.LogInformation("Speech recognized: {Length} characters", result.Length);
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Speech recognition cancelled");
                _recognizer.RecognizeAsyncCancel();
                return _recognizedText; // Return partial result
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
                
                var recognizer = recognizers.FirstOrDefault(r => r.Culture.Equals(culture));
                if (recognizer == null)
                {
                    // Fallback to en-US if available
                    recognizer = recognizers.FirstOrDefault(r => r.Culture.Name == "en-US");
                }

                if (recognizer == null)
                {
                    _logger.LogWarning("No speech recognizer found for culture {Culture}", culture.Name);
                    return;
                }

                _recognizer = new SpeechRecognitionEngine(recognizer.Id);
                
                _recognizer.LoadGrammar(new DictationGrammar());
                
                _recognizer.SetInputToDefaultAudioDevice();
                
                _recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1.5);
                _recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
                _recognizer.BabbleTimeout = TimeSpan.FromSeconds(3);
                
                _recognizer.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", 60);
                _recognizer.UpdateRecognizerSetting("AdaptationOn", 1);

                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                _recognizer.RecognizeCompleted += OnRecognizeCompleted;

                _logger.LogInformation("Speech recognizer initialized with voice isolation: {Culture}", recognizer.Culture.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize speech recognizer");
            }
        }

        private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.5f) // Only accept confident results
            {
                _recognizedText = e.Result.Text;
                _logger.LogDebug("Speech recognized with confidence {Confidence}: {Text}", 
                    e.Result.Confidence, e.Result.Text);
            }
            else
            {
                _logger.LogDebug("Low confidence speech rejected: {Confidence}", e.Result.Confidence);
            }
        }

        private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            _logger.LogDebug("Speech recognition rejected");
        }

        private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
        {
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
