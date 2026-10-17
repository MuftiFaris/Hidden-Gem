using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Assistant.Helpers;
using Assistant.Services;
using Microsoft.Extensions.Logging;

namespace Assistant
{
    public partial class OverlayWindow : Window
    {
        private readonly IGeminiService _gemini;
        private readonly IScreenCaptureService _screenCapture;
        private readonly ISpeechService _speech;
        private readonly ICredentialService _creds;
        private readonly ISettingsService? _settings;
        private readonly ILogger<OverlayWindow> _logger;
        private bool _isPinned;

        public OverlayWindow(
            IGeminiService gemini,
            IScreenCaptureService screenCapture,
            ISpeechService speech,
            ICredentialService creds,
            ILogger<OverlayWindow> logger, ISettingsService? settings = null)
        {
            _gemini = gemini;
            _screenCapture = screenCapture;
            _speech = speech;
            _creds = creds;
            _logger = logger;
            _settings = settings;

            InitializeComponent();
            
            this.Opacity = 0.95;
            OpacitySlider.Value = 0.95;
            
            this.SourceInitialized += OverlayWindow_SourceInitialized;
        }

        private void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            WindowPrivacyHelper.SetPrivacyMode(hwnd, true, _logger);
            _logger.LogInformation("Overlay privacy mode enabled");
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            this.Topmost = _isPinned;
            _logger.LogInformation("Overlay {State}", _isPinned ? "pinned" : "unpinned");
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsInitialized)
                this.Opacity = e.NewValue;
        }

        private async void VoiceButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                if (!_speech.IsAvailable())
                {
                    ResponseText.Text = "❌ Speech recognition not available on this system.";
                    return;
                }

                ResponseText.Text = "🎤 Listening... (speak now)";
                
                var speechText = await _speech.RecognizeSpeechAsync();
                
                if (string.IsNullOrWhiteSpace(speechText))
                {
                    ResponseText.Text = "❌ No speech detected.";
                    return;
                }

                ResponseText.Text = $"You said: {speechText}\n\nProcessing...";

                var settings = _settings?.Load() ?? new Models.AppSettings();
                var apiKey = _creds.GetApiKey(settings.Provider);
                if (string.IsNullOrEmpty(apiKey))
                {
                    ResponseText.Text = "❌ No API key configured.";
                    return;
                }

                var response = await _gemini.SendMessageAsync(
                    new System.Collections.Generic.List<Models.ChatMessage>
                    {
                        new() { Role = Models.MessageRole.User, Content = speechText }
                    },
                    apiKey,
                    settings);

                ResponseText.Text = $"You: {speechText}\n\nAssistant: {response}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice input failed");
                ResponseText.Text = $"❌ Error: {ex.Message}";
            }
            finally { if (button != null) button.IsEnabled = true; }
        }

        private async void ScreenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResponseText.Text = "📷 Capturing center screen...";

                var screenshot = await _screenCapture.CaptureCenterScreenAsync(0.70, 0.70);
                var base64 = _screenCapture.BitmapToBase64(screenshot);
                screenshot.Dispose();

                var settings = _settings?.Load() ?? new Models.AppSettings();
                var apiKey = _creds.GetApiKey(settings.Provider);
                if (string.IsNullOrEmpty(apiKey))
                {
                    ResponseText.Text = "❌ No API key configured.";
                    return;
                }

                ResponseText.Text = "🤖 Solving interview question / case...";

                
                var prompt = @"TARGET: Solve live technical interview question / test displayed in the screenshot.

INSTRUCTIONS:
1. Focus directly on the problem, case study, multiple choice question, or code snippet in the center of the screen.
2. Provide the DIRECT ANSWER immediately.
3. If code or algorithm is requested, write clean, optimal, production-ready code with minimal explanation.
4. If multiple choice / scenario, state the EXACT correct option and 1-sentence crisp reason.

FORMAT:
🎯 **Direct Answer / TL;DR**: (What candidate should say immediately)
💻 **Code / Detailed Solution**: (If applicable)
💡 **Key Explanation Points**: (Brief bullet points for follow-up questions)

RESPONSE:";

                var response = await _gemini.SendVisionMessageAsync(
                    prompt,
                    base64,
                    apiKey,
                    settings);

                ResponseText.Text = response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Screen capture failed");
                ResponseText.Text = $"❌ Error: {ex.Message}";
            }
        }
    }
}
