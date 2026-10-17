using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant.ViewModels
{
    public sealed class ChatViewModel : BaseViewModel
    {
        private readonly IGeminiService    _gemini;
        private readonly ICredentialService _creds;
        private readonly ISettingsService   _settingsSvc;
        private readonly ILogger<ChatViewModel> _logger;

        private string   _inputText  = string.Empty;
        private bool     _isSending;
        private int      _tokenCount;
        private AppSettings _settings;
        private CancellationTokenSource? _cts;

        private string _sendStatus = "";
        public string SendStatus { get => _sendStatus; private set => SetProperty(ref _sendStatus, value); }

        // ── Bindable state ─────────────────────────────────────────────────────

        /// <summary>The full conversation shown in the chat list.</summary>
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public string InputText
        {
            get => _inputText;
            set { SetProperty(ref _inputText, value); SendCommand.RaiseCanExecuteChanged(); }
        }

        public bool IsSending
        {
            get => _isSending;
            private set
            {
                SetProperty(ref _isSending, value);
                SendCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }

        public int TokenCount
        {
            get => _tokenCount;
            private set => SetProperty(ref _tokenCount, value);
        }

        // ── Commands ───────────────────────────────────────────────────────────

        public RelayCommand SendCommand   { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ClearCommand  { get; }

        // ── Constructor ────────────────────────────────────────────────────────

        public ChatViewModel(
            IGeminiService     gemini,
            ICredentialService creds,
            ISettingsService   settingsSvc,
            ILogger<ChatViewModel> logger)
        {
            _gemini      = gemini;
            _creds       = creds;
            _settingsSvc = settingsSvc;
            _logger      = logger;
            _settings    = settingsSvc.Load();

            SendCommand = new RelayCommand(
                _ => _ = SendAsync(),
                _ => !IsSending && !string.IsNullOrWhiteSpace(InputText));

            CancelCommand = new RelayCommand(
                _ => _cts?.Cancel(),
                _ => IsSending);

            ClearCommand = new RelayCommand(
                _ => { Messages.Clear(); TokenCount = 0; },
                _ => !IsSending && Messages.Count > 0);
        }

        /// <summary>Re-reads settings before sending (picks up model / temperature changes).</summary>
        public void RefreshSettings() => _settings = _settingsSvc.Load();

        // ── Send logic ─────────────────────────────────────────────────────────

        private async Task SendAsync()
        {
            RefreshSettings();
            var requestSettings = _settings;
            var apiKey = _creds.GetApiKey(requestSettings.Provider);
            if (string.IsNullOrEmpty(apiKey))
            {
                AddSystemMessage(
                    $"No {requestSettings.Provider} API key found. Save it in Settings first.");
                return;
            }

            // 1. Add user bubble immediately
            var userMsg = new ChatMessage { Role = MessageRole.User, Content = InputText.Trim() };
            Messages.Add(userMsg);
            InputText = string.Empty;
            IsSending = true;

            // 2. Add a placeholder assistant bubble
            var assistantMsg = new ChatMessage
            {
                Role      = MessageRole.Assistant,
                ProviderName = requestSettings.Provider,
                IsStreaming = true,
                Content   = string.Empty
            };
            Messages.Add(assistantMsg);

            _cts = new CancellationTokenSource();
            var requestCts = _cts;
            var token = requestCts.Token;
            var clock = Stopwatch.StartNew();
            bool firstText = false;
            SendStatus = $"Waiting for {requestSettings.Provider} / {requestSettings.SelectedModel}...";
            var statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            statusTimer.Tick += (_, _) => SendStatus = $"{(firstText ? "Receiving" : "Waiting for")} {requestSettings.Provider} · {clock.Elapsed.TotalSeconds:0}s";
            statusTimer.Start();

            // Build history — exclude the empty assistant placeholder
            var history = new List<ChatMessage>(Messages);
            history.RemoveAt(history.Count - 1);

            var dispatcher = Application.Current.Dispatcher;

            try
            {
                if (requestSettings.UseStreaming)
                {
                    await foreach (var chunk in _gemini.SendMessageStreamAsync(history, apiKey, requestSettings, token)
                                                       .ConfigureAwait(false))
                    {
                        if (!firstText)
                        {
                            firstText = true;
                            _logger.LogInformation("First response text after {Ms}ms [Provider: {Provider}, Model: {Model}]", clock.ElapsedMilliseconds, requestSettings.Provider, requestSettings.SelectedModel);
                        }
                        // All UI updates dispatched to the UI thread
                        await dispatcher.InvokeAsync(() => assistantMsg.Content += chunk);
                    }
                }
                else
                {
                    var reply = await _gemini.SendMessageAsync(history, apiKey, requestSettings, token)
                                            .ConfigureAwait(false);
                    await dispatcher.InvokeAsync(() => assistantMsg.Content = reply);
                }

                _logger.LogDebug("Exchange completed ({Chars} chars)",
                    assistantMsg.Content.Length);
            }
            catch (OperationCanceledException)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    assistantMsg.Content = "[Response cancelled by user]";
                    assistantMsg.IsError = true;
                    assistantMsg.IsStreaming = false;
                });
                _logger.LogInformation("Request was cancelled");
            }
            catch (GeminiApiException ex)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    assistantMsg.Content  = FormatApiError(ex);
                    assistantMsg.IsError  = true;
                    assistantMsg.IsStreaming = false;
                });
                _logger.LogError("Gemini API error {Code}: {Msg}", ex.HttpStatusCode, ex.Message);
            }
            catch (Exception ex)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    assistantMsg.Content  = $"❌  Unexpected error: {ex.Message}";
                    assistantMsg.IsError  = true;
                    assistantMsg.IsStreaming = false;
                });
                _logger.LogError(ex, "Unexpected error in SendAsync");
            }
            finally
            {
                await dispatcher.InvokeAsync(() =>
                {
                    assistantMsg.IsStreaming = false;
                    statusTimer.Stop();
                    requestCts.Dispose();
                    if (_cts == requestCts) _cts = null;
                    SendStatus = $"Finished in {clock.Elapsed.TotalSeconds:0.0}s";
                    IsSending = false;
                });
                _logger.LogInformation("Chat request finished after {Ms}ms [Provider: {Provider}, Model: {Model}]", clock.ElapsedMilliseconds, requestSettings.Provider, requestSettings.SelectedModel);
            }
        }

        private static string FormatApiError(GeminiApiException ex)
            => $"API error ({ex.HttpStatusCode}): {ex.Message}";

        private void AddSystemMessage(string text) =>
            Messages.Add(new ChatMessage { Role = MessageRole.System, Content = text });
    }
}
