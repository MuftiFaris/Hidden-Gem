using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels
{
    /// <summary>
    /// ViewModel for interview assistance features.
    /// Manages:
    ///   - Audio capture from Zoom/Discord/GMeet
    ///   - Auto-response detection + generation
    ///   - Question/response history display
    /// </summary>
    public sealed class InterviewViewModel : BaseViewModel
    {
        private readonly IAudioCaptureService _audioCapture;
        private readonly IAudioTranscriptionService _transcription;
        private readonly IAutoResponseService _autoResponse;
        private readonly ICredentialService _creds;
        private readonly ISettingsService _settingsSvc;
        private readonly ILogger<InterviewViewModel> _logger;

        private ObservableCollection<InterviewExchange> _exchanges = new();
        private bool _isCapturing;
        private bool _isAutoResponseEnabled;
        private string _currentQuestion = string.Empty;
        private string _currentResponse = string.Empty;
        private string _statusMessage = string.Empty;
        private CancellationTokenSource? _cts;

        // Commands
        public ICommand StartCaptureCommand { get; }
        public ICommand StopCaptureCommand { get; }
        public ICommand ToggleAutoResponseCommand { get; }
        public ICommand CopyResponseCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public InterviewViewModel(
            IAudioCaptureService audioCapture,
            IAudioTranscriptionService transcription,
            IAutoResponseService autoResponse,
            ICredentialService creds,
            ISettingsService settingsSvc,
            ILogger<InterviewViewModel> logger)
        {
            _audioCapture = audioCapture;
            _transcription = transcription;
            _autoResponse = autoResponse;
            _creds = creds;
            _settingsSvc = settingsSvc;
            _logger = logger;

            // Wire up event handlers
            _audioCapture.IsRecordingChanged += (_, isRecording) =>
            {
                IsCapturing = isRecording;
            };

            _autoResponse.ResponseGenerated += (_, args) =>
            {
                CurrentQuestion = args.Question;
                CurrentResponse = args.Response;
                Exchanges.Add(new InterviewExchange
                {
                    Timestamp = args.Timestamp,
                    Question = args.Question,
                    Response = args.Response
                });
            };

            _autoResponse.Error += (_, error) =>
            {
                StatusMessage = $"❌ Error: {error}";
            };

            // Commands
            StartCaptureCommand = new RelayCommand(_ => StartCapture(), _ => !IsCapturing);
            StopCaptureCommand = new RelayCommand(_ => StopCapture(), _ => IsCapturing);
            ToggleAutoResponseCommand = new RelayCommand(_ => ToggleAutoResponse());
            CopyResponseCommand = new RelayCommand(_ => CopyResponse(), _ => !string.IsNullOrEmpty(CurrentResponse));
            ClearHistoryCommand = new RelayCommand(_ => Exchanges.Clear());
        }

        // ── Bindable Properties ────────────────────────────────────────────────

        public ObservableCollection<InterviewExchange> Exchanges
        {
            get => _exchanges;
            private set => SetProperty(ref _exchanges, value);
        }

        public bool IsCapturing
        {
            get => _isCapturing;
            private set => SetProperty(ref _isCapturing, value);
        }

        public bool IsAutoResponseEnabled
        {
            get => _isAutoResponseEnabled;
            private set => SetProperty(ref _isAutoResponseEnabled, value);
        }

        public string CurrentQuestion
        {
            get => _currentQuestion;
            private set => SetProperty(ref _currentQuestion, value);
        }

        public string CurrentResponse
        {
            get => _currentResponse;
            private set => SetProperty(ref _currentResponse, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void StartCapture()
        {
            Task.Run(async () =>
            {
                try
                {
                    StatusMessage = "🎤 Starting audio capture...";
                    var success = await _audioCapture.StartCapturingAsync().ConfigureAwait(false);

                    if (success)
                    {
                        StatusMessage = "🎤 Listening for questions...";
                        _logger.LogInformation("Audio capture started");

                        // Start monitoring for questions
                        await MonitorForQuestionsAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        StatusMessage = "❌ Failed to start audio capture. Check microphone permissions.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start capture");
                    StatusMessage = $"❌ Error: {ex.Message}";
                }
            });
        }

        private void StopCapture()
        {
            Task.Run(async () =>
            {
                try
                {
                    _cts?.Cancel();
                    var samples = await _audioCapture.StopCapturingAsync().ConfigureAwait(false);
                    StatusMessage = "⏹️  Audio capture stopped";
                    _logger.LogInformation("Audio capture stopped. Captured {Samples} samples", samples.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to stop capture");
                    StatusMessage = $"❌ Error: {ex.Message}";
                }
            });
        }

        private void ToggleAutoResponse()
        {
            if (_autoResponse.IsEnabled)
            {
                _autoResponse.Disable();
                IsAutoResponseEnabled = false;
                StatusMessage = "🔴 Auto-response disabled";
            }
            else
            {
                _autoResponse.Enable();
                IsAutoResponseEnabled = true;
                StatusMessage = "🟢 Auto-response enabled";
            }
        }

        private void CopyResponse()
        {
            try
            {
                System.Windows.Clipboard.SetText(CurrentResponse);
                StatusMessage = "✅ Response copied to clipboard";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy response");
                StatusMessage = "❌ Failed to copy response";
            }
        }

        /// <summary>
        /// Monitors audio buffer for detected questions.
        /// Periodically checks if audio contains detectable question patterns.
        /// </summary>
        private async Task MonitorForQuestionsAsync()
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                while (!ct.IsCancellationRequested && IsCapturing)
                {
                    // Check every 3 seconds
                    await Task.Delay(3000, ct).ConfigureAwait(false);

                    if (!IsCapturing || !_autoResponse.IsEnabled) continue;

                    // Get current audio buffer
                    var buffer = _audioCapture.GetCurrentBuffer();

                    if (buffer.Length > 0)
                    {
                        // Transcribe audio
                        var apiKey = _creds.GetApiKey();
                        if (string.IsNullOrEmpty(apiKey)) continue;

                        var text = await _transcription.TranscribeAudioAsync(
                            buffer, apiKey, ct: ct).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(text))
                        {
                            // Check if this is a question
                            var rule = _autoResponse.DetectQuestion(text);
                            if (rule != null)
                            {
                                _logger.LogInformation("Question detected: {Question}", text);

                                // Clear buffer for next capture
                                _audioCapture.ClearBuffer();

                                // Generate response
                                var settings = _settingsSvc.Load();
                                await _autoResponse.GenerateResponseAsync(
                                    text, null, apiKey, settings, ct: ct).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Question monitoring cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in question monitoring");
                StatusMessage = $"❌ Monitoring error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Interview Q&A history entry.
    /// </summary>
    public class InterviewExchange
    {
        public DateTime Timestamp { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;

        public override string ToString() => $"{Timestamp:HH:mm:ss} | {Question}";
    }
}
