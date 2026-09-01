using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels
{
    public sealed class SettingsViewModel : BaseViewModel
    {
        private readonly ICredentialService _creds;
        private readonly ISettingsService   _settingsSvc;
        private readonly IGeminiService     _gemini;
        private readonly ILogger<SettingsViewModel> _logger;

        // ── Validation caching (prevent rate limit) ──────────────────────────
        private string _lastValidatedKey = string.Empty;
        private bool _lastValidationResult;
        private DateTime _lastValidationTime = DateTime.MinValue;
        private const int ValidationCacheDurationMs = 300000;  // 5 minutes

        // ── API Key state ──────────────────────────────────────────────────────

        private string _apiKeyInput        = string.Empty;
        private string _apiKeyStatus       = string.Empty;
        private bool   _hasApiKey;
        private bool   _isValidating;
        private bool   _isApiKeyStatusError;

        public string ApiKeyInput
        {
            get => _apiKeyInput;
            set
            {
                SetProperty(ref _apiKeyInput, value);
                SaveApiKeyCommand.RaiseCanExecuteChanged();
                ValidateApiKeyCommand.RaiseCanExecuteChanged();
            }
        }

        public string ApiKeyStatus
        {
            get => _apiKeyStatus;
            private set => SetProperty(ref _apiKeyStatus, value);
        }

        public bool HasApiKey
        {
            get => _hasApiKey;
            private set { SetProperty(ref _hasApiKey, value); DeleteApiKeyCommand.RaiseCanExecuteChanged(); }
        }

        public bool IsValidating
        {
            get => _isValidating;
            private set => SetProperty(ref _isValidating, value);
        }

        public bool IsApiKeyStatusError
        {
            get => _isApiKeyStatusError;
            private set => SetProperty(ref _isApiKeyStatusError, value);
        }

        // ── Model settings (two-way bound to controls) ─────────────────────────

        private string _selectedModel   = "gemini-3.5-flash";
        private double _temperature     = 0.7;
        private int    _maxTokens       = 2048;
        private bool   _useStreaming    = true;
        private string _systemPrompt    = string.Empty;
        private bool   _minimizeToTray  = true;
        private bool   _startMinimized;
        private bool   _enableLogging   = true;
        private bool   _logConversations;

        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }

        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        public bool UseStreaming
        {
            get => _useStreaming;
            set => SetProperty(ref _useStreaming, value);
        }

        public string SystemPrompt
        {
            get => _systemPrompt;
            set => SetProperty(ref _systemPrompt, value);
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

        public bool EnableLogging
        {
            get => _enableLogging;
            set => SetProperty(ref _enableLogging, value);
        }

        public bool LogConversations
        {
            get => _logConversations;
            set => SetProperty(ref _logConversations, value);
        }

        // ── Static lists ───────────────────────────────────────────────────────

        public List<string> AvailableModels { get; } = new()
        {
            "gemini-3.5-flash",
            "gemini-3.6-flash",
            "gemini-pro-latest",
            "gemini-3.5-pro",
        };

        // ── Commands ───────────────────────────────────────────────────────────

        public AsyncRelayCommand SaveApiKeyCommand     { get; }
        public RelayCommand      DeleteApiKeyCommand   { get; }
        public AsyncRelayCommand ValidateApiKeyCommand { get; }
        public RelayCommand      SaveSettingsCommand   { get; }
        public RelayCommand      ResetSettingsCommand  { get; }

        // ── Constructor ────────────────────────────────────────────────────────

        public SettingsViewModel(
            ICredentialService creds,
            ISettingsService   settingsSvc,
            IGeminiService     gemini,
            ILogger<SettingsViewModel> logger)
        {
            _creds       = creds;
            _settingsSvc = settingsSvc;
            _gemini      = gemini;
            _logger      = logger;

            SaveApiKeyCommand = new AsyncRelayCommand(
                _ => SaveApiKeyAsync(),
                _ => !string.IsNullOrWhiteSpace(ApiKeyInput));

            DeleteApiKeyCommand = new RelayCommand(
                _ => DeleteApiKey(),
                _ => HasApiKey);

            ValidateApiKeyCommand = new AsyncRelayCommand(
                _ => ValidateApiKeyAsync(),
                _ => !string.IsNullOrWhiteSpace(ApiKeyInput) && !IsValidating);

            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());

            ResetSettingsCommand = new RelayCommand(_ => LoadSettings(new AppSettings()));

            LoadState();
        }

        // ── Initialisation ─────────────────────────────────────────────────────

        private void LoadState()
        {
            HasApiKey = _creds.HasApiKey();
            ApiKeyStatus = HasApiKey
                ? "✅  API key is stored in Windows Credential Manager"
                : "No API key stored yet";
            LoadSettings(_settingsSvc.Load());
        }

        private void LoadSettings(AppSettings s)
        {
            SelectedModel  = s.SelectedModel;
            Temperature    = s.Temperature;
            MaxTokens      = s.MaxOutputTokens;
            UseStreaming   = s.UseStreaming;
            SystemPrompt   = s.SystemPrompt;
            MinimizeToTray = s.MinimizeToTrayOnClose;
            StartMinimized = s.StartMinimized;
            EnableLogging  = s.EnableLogging;
            LogConversations = s.LogConversations;
        }

        // ── API key operations ─────────────────────────────────────────────────

        private async Task SaveApiKeyAsync()
        {
            var key = ApiKeyInput.Trim();
            bool ok = _creds.SaveApiKey(key);
            if (ok)
            {
                HasApiKey            = true;
                IsApiKeyStatusError  = false;
                ApiKeyStatus         = "✅  API key saved to Windows Credential Manager";
                ApiKeyInput          = string.Empty;
                _logger.LogInformation("API key saved");
            }
            else
            {
                IsApiKeyStatusError = true;
                ApiKeyStatus        = "❌  Failed to save API key. Check app permissions.";
            }
            await Task.CompletedTask;
        }

        private void DeleteApiKey()
        {
            _creds.DeleteApiKey();
            HasApiKey           = false;
            IsApiKeyStatusError = false;
            ApiKeyStatus        = "API key removed";
            _logger.LogInformation("API key deleted");
        }

        private async Task ValidateApiKeyAsync()
        {
            var key = ApiKeyInput.Trim();
            
            if (string.IsNullOrWhiteSpace(key))
            {
                IsApiKeyStatusError = true;
                ApiKeyStatus = "❌  Please enter an API key";
                return;
            }

            if (!key.StartsWith("AIza", StringComparison.Ordinal))
            {
                IsApiKeyStatusError = true;
                ApiKeyStatus = "❌  Invalid format. Gemini API keys start with 'AIza'";
                return;
            }

            // Check cache first - prevent rate limiting from repeated tests
            if (key == _lastValidatedKey && 
                (DateTime.UtcNow - _lastValidationTime).TotalMilliseconds < ValidationCacheDurationMs)
            {
                ApiKeyStatus = _lastValidationResult 
                    ? "✅  API key is valid! (cached - retest in 5 min)"
                    : "❌  API key validation failed. (cached result)";
                IsApiKeyStatusError = !_lastValidationResult;
                _logger.LogInformation("API key validation result from cache");
                return;
            }

            IsValidating = true;
            ApiKeyStatus = "🔄  Validating API key…";
            IsApiKeyStatusError = false;

            try
            {
                bool valid = await _gemini.ValidateApiKeyAsync(key);

                // Cache the result
                _lastValidatedKey = key;
                _lastValidationResult = valid;
                _lastValidationTime = DateTime.UtcNow;

                IsValidating        = false;
                IsApiKeyStatusError = !valid;
                ApiKeyStatus        = valid
                    ? "✅  API key is valid! You can now save it."
                    : "❌  API key validation failed. Check format or permissions.";
                
                if (valid)
                {
                    _logger.LogInformation("API key validated successfully");
                }
            }
            catch (Exception ex)
            {
                IsValidating = false;
                IsApiKeyStatusError = true;
                
                // Check if it's a rate limit error
                if (ex.Message.Contains("429") || ex.Message.Contains("rate limit") || ex.Message.Contains("Rate limited"))
                {
                    ApiKeyStatus = "⏱️  Rate limit hit! Free tier: 15 requests/minute. Wait 1 minute and try again.";
                }
                else
                {
                    ApiKeyStatus = $"❌  Validation error: {ex.Message}";
                }
                
                _logger.LogError(ex, "API key validation failed");
            }
        }

        // ── Settings persistence ───────────────────────────────────────────────

        private void SaveSettings()
        {
            var s = new AppSettings
            {
                SelectedModel    = SelectedModel,
                Temperature      = Temperature,
                MaxOutputTokens  = MaxTokens,
                UseStreaming     = UseStreaming,
                SystemPrompt     = SystemPrompt,
                MinimizeToTrayOnClose = MinimizeToTray,
                StartMinimized   = StartMinimized,
                EnableLogging    = EnableLogging,
                LogConversations = LogConversations,
            };
            _settingsSvc.Save(s);
            _logger.LogInformation("Settings saved");
        }

        /// <summary>Raised by MainViewModel after settings are saved so chat picks up changes.</summary>
        public event EventHandler? SettingsSaved;
        public void NotifySettingsSaved() => SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
