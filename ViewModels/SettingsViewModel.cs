using System.Collections.ObjectModel;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels;

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly ICredentialService _creds;
    private readonly ISettingsService _settings;
    private readonly IGeminiService _gemini;
    private readonly ILogger<SettingsViewModel> _logger;
    private string _apiKeyInput = "", _status = "", _settingsStatus = "";
    private bool _hasKey, _busy, _error, _inputErrors;
    private string _model = "auto", _prompt = "";
    private double _temperature = 0.7;
    private int _tokens = 2048, _timeout = 45;
    private string _provider = "Gemini", _routerUrl = "http://localhost:20128/v1", _speechModel = "";
    private string _geminiModel = "auto", _routerModel = "";
    public IReadOnlyList<string> AvailableProviders { get; } = new[] { "Gemini", "9Router" };
    public string Provider
    {
        get => _provider;
        set
        {
            if (value == _provider || value is not ("Gemini" or "9Router")) return;
            if (_provider == "Gemini") _geminiModel = SelectedModel; else _routerModel = SelectedModel;
            SetProperty(ref _provider, value);
            OnPropertyChanged(nameof(IsRouterProvider)); OnPropertyChanged(nameof(IsGeminiProvider));
            ApiKeyInput = ""; HasApiKey = _creds.HasApiKey(Provider);
            AvailableModels.Clear();
            if (IsGeminiProvider) AvailableModels.Add("auto");
            SelectedModel = IsRouterProvider ? _routerModel : _geminiModel;
            if (!string.IsNullOrWhiteSpace(SelectedModel) && !AvailableModels.Contains(SelectedModel)) AvailableModels.Add(SelectedModel);
            ApiKeyStatus = HasApiKey ? "Key saved for this provider. Refresh models and test the connection." : "Save this provider's API key, then refresh models.";
            RefreshCommands();
        }
    }
    public bool IsRouterProvider => Provider == "9Router";
    public bool IsGeminiProvider => !IsRouterProvider;
    public string RouterBaseUrl { get => _routerUrl; set => SetProperty(ref _routerUrl, value); }
    public string RouterTranscriptionModel { get => _speechModel; set => SetProperty(ref _speechModel, value); }
    public int RequestTimeoutSeconds { get => _timeout; set => SetProperty(ref _timeout, value); }
    private AppSettings ConnectionSettings() => new() { Provider = Provider, RouterBaseUrl = RouterBaseUrl, RouterTranscriptionModel = RouterTranscriptionModel, RequestTimeoutSeconds = RequestTimeoutSeconds, SelectedModel = SelectedModel };
    private bool _stream = true, _tray = true, _startMinimized, _logging = true, _logConversations;
    public ObservableCollection<string> AvailableModels { get; } = new() { "auto" };
    public string ApiKeyInput { get => _apiKeyInput; set { SetProperty(ref _apiKeyInput, value); RefreshCommands(); } }
    public string ApiKeyStatus { get => _status; private set => SetProperty(ref _status, value); }
    public string SettingsStatus { get => _settingsStatus; private set => SetProperty(ref _settingsStatus, value); }
    public bool HasApiKey { get => _hasKey; private set { SetProperty(ref _hasKey, value); RefreshCommands(); } }
    public bool IsValidating { get => _busy; private set { SetProperty(ref _busy, value); OnPropertyChanged(nameof(CanEditConnection)); RefreshCommands(); } }
    public bool HasInputErrors { get => _inputErrors; set { SetProperty(ref _inputErrors, value); RefreshCommands(); if (value) SettingsStatus = "Enter whole numbers for tokens and request timeout."; } }
    public bool CanEditConnection => !IsValidating;
    public bool IsApiKeyStatusError { get => _error; private set => SetProperty(ref _error, value); }
    public string SelectedModel { get => _model; set { SetProperty(ref _model, value); ApiKeyStatus = HasApiKey ? "Key saved. Test this model to verify generation and quota." : "Save a key, then test the connection."; } }
    public double Temperature { get => _temperature; set => SetProperty(ref _temperature, value); }
    public int MaxTokens { get => _tokens; set => SetProperty(ref _tokens, value); }
    public bool UseStreaming { get => _stream; set => SetProperty(ref _stream, value); }
    public string SystemPrompt { get => _prompt; set => SetProperty(ref _prompt, value); }
    public bool MinimizeToTray { get => _tray; set => SetProperty(ref _tray, value); }
    public bool StartMinimized { get => _startMinimized; set => SetProperty(ref _startMinimized, value); }
    public bool EnableLogging { get => _logging; set => SetProperty(ref _logging, value); }
    public bool LogConversations { get => _logConversations; set => SetProperty(ref _logConversations, value); }
    public AsyncRelayCommand SaveApiKeyCommand { get; }
    public RelayCommand DeleteApiKeyCommand { get; }
    public AsyncRelayCommand ValidateApiKeyCommand { get; }
    public AsyncRelayCommand RefreshModelsCommand { get; }
    public RelayCommand UseRecommendedModelCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }
    public RelayCommand ResetPersonaCommand { get; }
    public event EventHandler? SettingsSaved;

    public SettingsViewModel(ICredentialService creds, ISettingsService settingsSvc, IGeminiService gemini, ILogger<SettingsViewModel> logger)
    {
        _creds = creds; _settings = settingsSvc; _gemini = gemini; _logger = logger;
        SaveApiKeyCommand = new AsyncRelayCommand(SaveKeyAsync, () => !IsValidating && !string.IsNullOrWhiteSpace(ApiKeyInput));
        DeleteApiKeyCommand = new RelayCommand(DeleteKey, () => HasApiKey && !IsValidating);
        ValidateApiKeyCommand = new AsyncRelayCommand(TestAsync, () => !IsValidating && (HasApiKey || !string.IsNullOrWhiteSpace(ApiKeyInput)));
        RefreshModelsCommand = new AsyncRelayCommand(RefreshModelsAsync, () => !IsValidating && (HasApiKey || !string.IsNullOrWhiteSpace(ApiKeyInput)));
        UseRecommendedModelCommand = new RelayCommand(() => { SelectedModel = "auto"; SaveSettings(); }, () => !IsValidating && IsGeminiProvider);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => !IsValidating && !HasInputErrors);
        ResetSettingsCommand = new RelayCommand(() => { LoadSettings(new()); SettingsStatus = "Defaults restored. Save to apply."; }, () => !IsValidating);
        ResetPersonaCommand = new RelayCommand(() => { SystemPrompt = AppSettings.DefaultSystemPrompt; SettingsStatus = "Default persona restored. Save settings to apply."; }, () => !IsValidating);
        HasApiKey = _creds.HasApiKey(Provider);
        LoadSettings(_settings.Load());
        ApiKeyStatus = HasApiKey ? "Key stored securely. Test the connection to verify quota." : "No API key saved.";
    }
    private void RefreshCommands()
    {
        SaveApiKeyCommand?.RaiseCanExecuteChanged(); DeleteApiKeyCommand?.RaiseCanExecuteChanged();
        ValidateApiKeyCommand?.RaiseCanExecuteChanged(); RefreshModelsCommand?.RaiseCanExecuteChanged();
        SaveSettingsCommand?.RaiseCanExecuteChanged(); ResetSettingsCommand?.RaiseCanExecuteChanged(); UseRecommendedModelCommand?.RaiseCanExecuteChanged(); ResetPersonaCommand?.RaiseCanExecuteChanged();
    }
    private string Key() => string.IsNullOrWhiteSpace(ApiKeyInput) ? _creds.GetApiKey(Provider) ?? "" : ApiKeyInput.Trim();
    private async Task SaveKeyAsync()
    {
        if (!_creds.SaveApiKey(ApiKeyInput.Trim(), Provider)) { IsApiKeyStatusError = true; ApiKeyStatus = "Could not save the key in Windows Credential Manager."; return; }
        HasApiKey = true; ApiKeyInput = ""; IsApiKeyStatusError = false;
        ApiKeyStatus = "Key saved. Loading models...";
        await RefreshModelsAsync();
    }
    private void DeleteKey()
    {
        if (!_creds.DeleteApiKey(Provider)) { IsApiKeyStatusError = true; ApiKeyStatus = "Could not remove the saved key."; return; }
        HasApiKey = false; ApiKeyInput = ""; IsApiKeyStatusError = false; ApiKeyStatus = "API key removed.";
    }
    private async Task LoadModelsAsync(string key, CancellationToken ct)
    {
        var models = await _gemini.GetAvailableModelsAsync(key, ct, ConnectionSettings());
        var selected = SelectedModel;
        AvailableModels.Clear(); if (IsGeminiProvider) AvailableModels.Add("auto");
        foreach (var model in models.OrderBy(m => m)) AvailableModels.Add(model);
        // Keep a saved, unavailable selection visible so the user can replace it deliberately.
        if (!string.IsNullOrWhiteSpace(selected) && !AvailableModels.Contains(selected)) AvailableModels.Add(selected);
        SelectedModel = selected;
    }
    private async Task RefreshModelsAsync()
    {
        IsValidating = true; IsApiKeyStatusError = false; ApiKeyStatus = "Loading models for this key...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await LoadModelsAsync(Key(), timeout.Token);
            ApiKeyStatus = "Models loaded. Model listing does not verify quota; test the connection next.";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { IsValidating = false; }
    }
    private async Task TestAsync()
    {
        IsValidating = true; IsApiKeyStatusError = false; ApiKeyStatus = "Testing generation with the selected model...";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var key = Key();
            await LoadModelsAsync(key, timeout.Token);
            var model = await _gemini.TestConnectionAsync(key, ConnectionSettings(), timeout.Token);
            ApiKeyStatus = $"Connected — {model} generated a response. Save settings to apply your model choice.";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { IsValidating = false; }
    }
    private void ReportError(Exception ex)
    {
        IsApiKeyStatusError = true;
        ApiKeyStatus = ex is OperationCanceledException ? "Connection test timed out. Check your connection and try again." : ex.Message;
        _logger.LogWarning("Connection check failed: {Message}", ApiKeyStatus);
    }
    private void LoadSettings(AppSettings settings)
    {
        _provider = settings.Provider == "9Router" ? "9Router" : "Gemini";
        OnPropertyChanged(nameof(Provider)); OnPropertyChanged(nameof(IsRouterProvider)); OnPropertyChanged(nameof(IsGeminiProvider));
        RouterBaseUrl = settings.RouterBaseUrl; RouterTranscriptionModel = settings.RouterTranscriptionModel;
        RequestTimeoutSeconds = settings.RequestTimeoutSeconds;
        _geminiModel = IsGeminiProvider ? settings.SelectedModel : settings.GeminiModel;
        _routerModel = IsRouterProvider ? settings.SelectedModel : settings.RouterModel;
        HasApiKey = _creds.HasApiKey(Provider);
        AvailableModels.Clear(); if (IsGeminiProvider) AvailableModels.Add("auto");
        var model = string.IsNullOrWhiteSpace(settings.SelectedModel) ? (IsGeminiProvider ? "auto" : "") : settings.SelectedModel;
        if (!string.IsNullOrWhiteSpace(model) && !AvailableModels.Contains(model)) AvailableModels.Add(model);
        SelectedModel = model; Temperature = settings.Temperature; MaxTokens = settings.MaxOutputTokens;
        HasInputErrors = false; OnPropertyChanged(nameof(MaxTokens)); OnPropertyChanged(nameof(RequestTimeoutSeconds));
        UseStreaming = settings.UseStreaming; SystemPrompt = settings.SystemPrompt;
        MinimizeToTray = settings.MinimizeToTrayOnClose; StartMinimized = settings.StartMinimized;
        EnableLogging = settings.EnableLogging; LogConversations = settings.LogConversations;
    }
    private void SaveSettings()
    {
        if (HasInputErrors) { SettingsStatus = "Correct the invalid input before saving."; return; }
        if (!double.IsFinite(Temperature) || Temperature < 0 || Temperature > 2 || MaxTokens < 128 || MaxTokens > 32768 || RequestTimeoutSeconds < 5 || RequestTimeoutSeconds > 120)
        { SettingsStatus = "Use temperature 0–2, tokens 128–32768, and request timeout 5–120 seconds."; return; }
        try
        {
            // Preserve preferences managed elsewhere, including the privacy toggle.
            var settings = _settings.Load();
            if (IsRouterProvider) OpenAiCompatibleService.NormalizeBaseUrl(RouterBaseUrl);
            settings.Provider = Provider; settings.RouterBaseUrl = RouterBaseUrl; settings.RouterTranscriptionModel = RouterTranscriptionModel;
            settings.RequestTimeoutSeconds = RequestTimeoutSeconds;
            if (IsGeminiProvider) _geminiModel = SelectedModel; else _routerModel = SelectedModel;
            settings.GeminiModel = _geminiModel; settings.RouterModel = _routerModel;
            settings.SelectedModel = SelectedModel; settings.Temperature = Temperature; settings.MaxOutputTokens = MaxTokens;
            settings.UseStreaming = UseStreaming; settings.SystemPrompt = SystemPrompt;
            settings.MinimizeToTrayOnClose = MinimizeToTray; settings.StartMinimized = StartMinimized;
            settings.EnableLogging = EnableLogging; settings.LogConversations = LogConversations;
            _settings.Save(settings); NotifySettingsSaved(); SettingsStatus = "Settings saved.";
        }
        catch (Exception ex) { SettingsStatus = "Settings could not be saved: " + ex.Message; }
    }
    public void NotifySettingsSaved() => SettingsSaved?.Invoke(this, EventArgs.Empty);
}
