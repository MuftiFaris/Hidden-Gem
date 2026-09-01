using System;
using System.Windows;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels
{
    /// <summary>
    /// Root view-model for MainWindow.
    ///  • Owns navigation between Chat and Settings views.
    ///  • Owns the Privacy Mode toggle and exposes its state to the window chrome.
    ///  • Coordinates settings-save → chat-refresh handshake.
    /// </summary>
    public sealed class MainViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsSvc;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IServiceProvider _services;

        private BaseViewModel _currentView;
        private bool  _isPrivacyModeEnabled;
        private bool  _isChatSelected = true;
        private string _statusMessage  = string.Empty;
        private OverlayWindow? _overlayWindow;

        // ── Child view-models (set by DI) ──────────────────────────────────────
        public ChatViewModel     ChatVM     { get; }
        public SettingsViewModel SettingsVM { get; }
        public InterviewViewModel InterviewVM { get; }

        // ── Bindable state ─────────────────────────────────────────────────────

        public BaseViewModel CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        public bool IsPrivacyModeEnabled
        {
            get => _isPrivacyModeEnabled;
            set
            {
                if (SetProperty(ref _isPrivacyModeEnabled, value))
                {
                    OnPrivacyModeChanged(value);
                    // Persist the preference
                    var s = _settingsSvc.Load();
                    s.PrivacyModeEnabled = value;
                    _settingsSvc.Save(s);
                }
            }
        }

        public bool IsChatSelected
        {
            get => _isChatSelected;
            private set => SetProperty(ref _isChatSelected, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── Window HWND — set by MainWindow after it loads ─────────────────────
        public IntPtr WindowHandle { get; set; }

        // ── Commands ───────────────────────────────────────────────────────────

        public RelayCommand NavigateToChatCommand     { get; }
        public RelayCommand NavigateToSettingsCommand { get; }
        public RelayCommand TogglePrivacyModeCommand  { get; }
        public RelayCommand ShowOverlayCommand        { get; }

        // ── Constructor ────────────────────────────────────────────────────────

        public MainViewModel(
            ChatViewModel      chatVM,
            SettingsViewModel  settingsVM,
            InterviewViewModel interviewVM,
            ISettingsService   settingsSvc,
            ILogger<MainViewModel> logger,
            IServiceProvider services)
        {
            ChatVM      = chatVM;
            SettingsVM  = settingsVM;
            InterviewVM = interviewVM;
            _settingsSvc = settingsSvc;
            _logger      = logger;
            _services    = services;

            _currentView = chatVM; // start on Chat

            NavigateToChatCommand = new RelayCommand(_ =>
            {
                CurrentView    = ChatVM;
                IsChatSelected = true;
                ChatVM.RefreshSettings();
            });

            NavigateToSettingsCommand = new RelayCommand(_ =>
            {
                CurrentView    = SettingsVM;
                IsChatSelected = false;
            });

            var navigateToInterviewCommand = new RelayCommand(_ =>
            {
                CurrentView    = InterviewVM;
                IsChatSelected = false;  // Interview is not Chat
            });

            TogglePrivacyModeCommand = new RelayCommand(_ =>
                IsPrivacyModeEnabled = !IsPrivacyModeEnabled);

            ShowOverlayCommand = new RelayCommand(_ =>
            {
                if (_overlayWindow == null || !_overlayWindow.IsVisible)
                {
                    _overlayWindow = (OverlayWindow)_services.GetService(typeof(OverlayWindow))!;
                    _overlayWindow.Closed += (s, e) => _overlayWindow = null;
                    _overlayWindow.Show();
                    StatusMessage = "Overlay window opened";
                }
                else
                {
                    _overlayWindow.Activate();
                }
            });

            // Load persisted preference
            var saved = settingsSvc.Load();
            _isPrivacyModeEnabled = saved.PrivacyModeEnabled;
            // The actual API call requires the HWND, so we apply it after the
            // window loads via ApplyInitialPrivacyMode().
        }

        // ── Privacy mode ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by MainWindow after the handle is available (SourceInitialized event).
        /// Applies the persisted privacy-mode preference immediately on startup.
        /// </summary>
        public void ApplyInitialPrivacyMode()
        {
            if (_isPrivacyModeEnabled)
                WindowPrivacyHelper.SetPrivacyMode(WindowHandle, true, _logger);
        }

        private void OnPrivacyModeChanged(bool enabled)
        {
            if (WindowHandle == IntPtr.Zero)
            {
                _logger.LogWarning("Privacy mode toggled before window handle was available");
                return;
            }

            bool ok = WindowPrivacyHelper.SetPrivacyMode(WindowHandle, enabled, _logger);
            StatusMessage = ok
                ? (enabled ? "🔒  Privacy mode ON — content hidden from most screen-capture tools"
                           : "🔓  Privacy mode OFF")
                : "⚠️  Privacy mode could not be applied on this Windows version";
        }
    }
}
