using System.Collections.ObjectModel;
using System.Windows;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels;

public sealed class InterviewViewModel : BaseViewModel
{
    private readonly IAudioCaptureService _audio;
    private readonly IAudioTranscriptionService _transcription;
    private readonly IScreenCaptureService _screen;
    private readonly LiveInterviewService _live;
    private readonly ICredentialService _creds;
    private readonly ISettingsService _settings;
    private readonly ILogger<InterviewViewModel> _logger;
    private CancellationTokenSource? _cts;
    private Task? _monitor;
    private bool _isCapturing;
    private bool _isStarting;
    private bool _autoResponse = true;
    private string _question = "";
    private string _response = "";
    private string _status = "Choose the window showing the question and the speaker/headphone output used by the call.";
    private ScreenWindow? _selectedWindow;
    private AudioDevice? _selectedAudio;

    public ObservableCollection<ScreenWindow> AvailableWindows { get; } = new();
    public ObservableCollection<AudioDevice> AvailableAudioDevices { get; } = new();
    public ObservableCollection<InterviewExchange> Exchanges { get; } = new();
    public ScreenWindow? SelectedWindow { get => _selectedWindow; set { SetProperty(ref _selectedWindow, value); StartCaptureCommand.RaiseCanExecuteChanged(); } }
    public AudioDevice? SelectedAudioDevice { get => _selectedAudio; set => SetProperty(ref _selectedAudio, value); }
    public bool IsCapturing { get => _isCapturing; private set { SetProperty(ref _isCapturing, value); OnPropertyChanged(nameof(CanSelectSources)); StartCaptureCommand.RaiseCanExecuteChanged(); StopCaptureCommand.RaiseCanExecuteChanged(); RefreshSourcesCommand.RaiseCanExecuteChanged(); } }
    public bool CanSelectSources => !IsCapturing;
    public bool IsAutoResponseEnabled { get => _autoResponse; set => SetProperty(ref _autoResponse, value); }
    public string CurrentQuestion { get => _question; private set => SetProperty(ref _question, value); }
    public string CurrentResponse { get => _response; private set { SetProperty(ref _response, value); CopyResponseCommand.RaiseCanExecuteChanged(); } }
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public AsyncRelayCommand StartCaptureCommand { get; }
    public AsyncRelayCommand StopCaptureCommand { get; }
    public RelayCommand RefreshSourcesCommand { get; }
    public RelayCommand ToggleAutoResponseCommand { get; }
    public RelayCommand CopyResponseCommand { get; }
    public RelayCommand ClearHistoryCommand { get; }

    public InterviewViewModel(IAudioCaptureService audio, IAudioTranscriptionService transcription,
        IScreenCaptureService screen, LiveInterviewService live, ICredentialService creds,
        ISettingsService settings, ILogger<InterviewViewModel> logger)
    {
        _audio = audio; _transcription = transcription; _screen = screen; _live = live;
        _creds = creds; _settings = settings; _logger = logger;
        StartCaptureCommand = new AsyncRelayCommand(StartAsync, () => !IsCapturing && SelectedWindow != null);
        StopCaptureCommand = new AsyncRelayCommand(StopAsync, () => IsCapturing && !_isStarting);
        RefreshSourcesCommand = new RelayCommand(RefreshSources, () => !IsCapturing);
        ToggleAutoResponseCommand = new RelayCommand(() => IsAutoResponseEnabled = !IsAutoResponseEnabled);
        CopyResponseCommand = new RelayCommand(() => Clipboard.SetText(CurrentResponse), () => !string.IsNullOrWhiteSpace(CurrentResponse));
        ClearHistoryCommand = new RelayCommand(() => Exchanges.Clear());
        RefreshSources();
    }

    private void RefreshSources()
    {
        try
        {
            var previous = SelectedWindow?.Handle;
            AvailableWindows.Clear();
            foreach (var window in _screen.GetAvailableWindows()) AvailableWindows.Add(window);
            SelectedWindow = AvailableWindows.FirstOrDefault(w => w.Handle == previous) ??
                AvailableWindows.FirstOrDefault(w => w.Title.Contains("Zoom", StringComparison.OrdinalIgnoreCase) || w.Title.Contains("Discord", StringComparison.OrdinalIgnoreCase) || w.Title.Contains("Meet", StringComparison.OrdinalIgnoreCase));
            var audioId = SelectedAudioDevice?.Id;
            AvailableAudioDevices.Clear();
            AvailableAudioDevices.Add(new AudioDevice { Id = "", Name = "Default speaker / headphones", IsLoopback = true });
            foreach (var device in _audio.GetAvailableDevices()) AvailableAudioDevices.Add(device);
            SelectedAudioDevice = AvailableAudioDevices.FirstOrDefault(d => d.Id == audioId) ?? AvailableAudioDevices[0];
        }
        catch (Exception ex) { StatusMessage = ex.Message; _logger.LogError(ex, "Unable to list capture sources"); }
    }

    private async Task StartAsync()
    {
        if (SelectedWindow == null) return;
        var connection = _settings.Load();
        var key = _creds.GetApiKey(connection.Provider);
        if (string.IsNullOrWhiteSpace(key)) { StatusMessage = $"Save your {connection.Provider} API key in Settings first."; return; }
        var window = SelectedWindow;
        _cts = new CancellationTokenSource();
        _isStarting = true;
        IsCapturing = true;
        try
        {
            bool needsStt = connection.Provider == "9Router" && string.IsNullOrWhiteSpace(connection.RouterTranscriptionModel);
            var audioStarted = !needsStt && await _audio.StartCapturingAsync(SelectedAudioDevice?.Id ?? "", _cts.Token);
            StatusMessage = audioStarted ? "Monitoring selected window + speaker audio." : needsStt ? "Monitoring selected window. Configure a 9Router STT model in Settings to enable speaker transcription." : "Monitoring selected window. Speaker capture unavailable; screen questions still work.";
            _monitor = MonitorAsync(window, key, audioStarted, connection, _cts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsCapturing = false;
            _cts.Dispose(); _cts = null;
        }
        finally
        {
            _isStarting = false;
            StopCaptureCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task StopAsync()
    {
        _cts?.Cancel();
        if (_monitor != null) await _monitor;
        await _audio.StopCapturingAsync();
        _monitor = null;
        _cts?.Dispose(); _cts = null;
        IsCapturing = false;
        StatusMessage = "Monitoring stopped.";
    }

    private async Task MonitorAsync(ScreenWindow window, string key, bool hasAudio, AppSettings connection, CancellationToken ct)
    {
        byte[]? lastScreen = null;
        var recent = new List<LiveInterviewAnswer>();
        var nextAnalysis = DateTime.MinValue;
        var utteranceStart = DateTime.MinValue;
        var consumedSpeech = DateTime.MinValue;
        var wasEnabled = false;
        string pendingTranscript = "";
        string? blockedReason = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(750, ct);
                    if (!IsAutoResponseEnabled)
                    {
                        _audio.ClearBuffer(); lastScreen = null; utteranceStart = DateTime.MinValue;
                        pendingTranscript = ""; wasEnabled = false; consumedSpeech = _audio.LastSpeechUtc;
                        StatusMessage = blockedReason ?? "Monitoring paused. Enable auto-response to continue.";
                        continue;
                    }
                    if (!wasEnabled) { lastScreen = null; wasEnabled = true; blockedReason = null; }
                    var currentSettings = _settings.Load();
                    if (currentSettings.Provider != connection.Provider || (connection.Provider == "9Router" && currentSettings.RouterBaseUrl != connection.RouterBaseUrl))
                    {
                        IsAutoResponseEnabled = false;
                        blockedReason = "Connection changed. Stop and restart monitoring to use the new provider/key.";
                        StatusMessage = blockedReason;
                        continue;
                    }
                    var now = DateTime.UtcNow;
                    if (hasAudio && _audio.LastSpeechUtc > consumedSpeech && utteranceStart == DateTime.MinValue)
                        utteranceStart = now;
                    bool speechFinished = utteranceStart != DateTime.MinValue &&
                        ((now - _audio.LastSpeechUtc).TotalSeconds >= 1.2 || (now - utteranceStart).TotalSeconds >= 15);
                    if (speechFinished && now >= nextAnalysis)
                    {
                        consumedSpeech = _audio.LastSpeechUtc;
                        var samples = _audio.TakeCurrentBuffer();
                        utteranceStart = DateTime.MinValue;
                        if (samples.Length > 0 && Math.Sqrt(samples.Average(x => x * x)) > 0.006)
                        {
                            StatusMessage = "Transcribing speaker audio...";
                            var text = await _transcription.TranscribeAudioAsync(samples, key, _audio.SampleRate, ct, currentSettings);
                            pendingTranscript = (pendingTranscript + " " + text).Trim();
                            if (pendingTranscript.Length > 6000) pendingTranscript = pendingTranscript[^6000..];
                        }
                    }
                    if (now < nextAnalysis) continue;
                    using var bitmap = await _screen.CaptureWindowAsync(window.Handle);
                    ct.ThrowIfCancellationRequested();
                    var signature = LiveInterviewService.ScreenSignature(bitmap);
                    bool changed = LiveInterviewService.ScreenChanged(lastScreen, signature);
                    if (!changed && pendingTranscript.Length == 0) continue;
                    // Preserve the full question area, including code/options, within the chosen window.
                    var image = _screen.BitmapToBase64(bitmap);
                    StatusMessage = pendingTranscript.Length > 0 ? "Reading question from screen + speaker audio..." : "Checking selected window for a new question...";
                    var answer = await _live.AnalyzeAsync(image, pendingTranscript, recent, key, currentSettings, ct);
                    ct.ThrowIfCancellationRequested();
                    if (!IsAutoResponseEnabled) { lastScreen = null; pendingTranscript = ""; continue; }
                    lastScreen = signature;
                    pendingTranscript = "";
                    nextAnalysis = DateTime.UtcNow.AddSeconds(8);
                    if (answer != null)
                    {
                        recent.Add(answer);
                        if (recent.Count > 20) recent.RemoveAt(0);
                        CurrentQuestion = answer.Question;
                        CurrentResponse = answer.Answer;
                        Exchanges.Add(new InterviewExchange { Timestamp = DateTime.Now, Question = answer.Question, Response = answer.Answer });
                        StatusMessage = "Answer ready. Monitoring for the next question.";
                    }
                    else StatusMessage = "Monitoring. No new question detected.";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Live interview analysis failed");
                    StatusMessage = $"{ex.Message} — monitoring will retry.";
                    if (ex is GeminiApiException { IsQuotaUnavailable: true })
                    {
                        IsAutoResponseEnabled = false;
                        blockedReason = ex.Message + " Auto-response paused; fix the quota/model, then re-enable it.";
                        StatusMessage = blockedReason;
                    }
                    // Avoid burning quota or stopping monitoring after a transient API failure.
                    nextAnalysis = DateTime.UtcNow.AddSeconds(ex is GeminiApiException api && api.HttpStatusCode == 429 ? 30 : 10);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}

public class InterviewExchange
{
    public DateTime Timestamp { get; set; }
    public string Question { get; set; } = "";
    public string Response { get; set; } = "";
    public override string ToString() => $"{Timestamp:HH:mm:ss} | {Question}";
}

