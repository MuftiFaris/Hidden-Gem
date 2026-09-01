using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        // ── Rate limiting (free tier: 15 requests/minute) ──────────────────────
        private DateTime _lastApiRequestTime = DateTime.MinValue;
        private const int MinMsPerRequest = 5000;  // 5 seconds per request (12 requests/minute) - safer than theoretical 15

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
            var apiKey = _creds.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                AddSystemMessage(
                    "⚠️  No API key found. Please go to Settings and enter your Gemini API key.");
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
                IsStreaming = true,
                Content   = string.Empty
            };
            Messages.Add(assistantMsg);

            // 3. Rate limiting: enforce min delay between requests (15 req/min = ~4100ms per request)
            var timeSinceLastRequest = (DateTime.UtcNow - _lastApiRequestTime).TotalMilliseconds;
            if (timeSinceLastRequest < MinMsPerRequest)
            {
                var delayMs = (int)(MinMsPerRequest - timeSinceLastRequest);
                _logger.LogInformation("Rate limit prevention: waiting {Ms}ms before next request", delayMs);
                await Task.Delay(delayMs).ConfigureAwait(false);
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Build history — exclude the empty assistant placeholder
            var history = new List<ChatMessage>(Messages);
            history.RemoveAt(history.Count - 1);

            var dispatcher = Application.Current.Dispatcher;

            try
            {
                // Record request time BEFORE making API call
                _lastApiRequestTime = DateTime.UtcNow;

                if (_settings.UseStreaming)
                {
                    await foreach (var chunk in _gemini.SendMessageStreamAsync(history, apiKey, _settings, token)
                                                       .ConfigureAwait(false))
                    {
                        // All UI updates dispatched to the UI thread
                        await dispatcher.InvokeAsync(() => assistantMsg.Content += chunk);
                    }
                }
                else
                {
                    var reply = await _gemini.SendMessageAsync(history, apiKey, _settings, token)
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
                    IsSending = false;
                });
                _cts?.Dispose();
                _cts = null;
            }
        }

        private static string FormatApiError(GeminiApiException ex) => ex.HttpStatusCode switch
        {
            400 => $"❌  Bad request: {ex.Message}",
            401 => "❌  Invalid API key. Please update it in Settings.",
            403 => "❌  Access denied. Check your API key permissions.",
            429 => "❌  Rate limit exceeded. Please wait a moment and try again.",
            500 => $"❌  Gemini server error: {ex.Message}",
            _   => $"❌  API error ({ex.HttpStatusCode}): {ex.Message}"
        };

        private void AddSystemMessage(string text) =>
            Messages.Add(new ChatMessage { Role = MessageRole.System, Content = text });
    }
}
