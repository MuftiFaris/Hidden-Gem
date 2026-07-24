using System;
using System.Windows;
using System.Windows.Input;
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
        private readonly ILogger<OverlayWindow> _logger;
        private bool _isPinned;

        public OverlayWindow(
            IGeminiService gemini,
            IScreenCaptureService screenCapture,
            ISpeechService speech,
            ICredentialService creds,
            ILogger<OverlayWindow> logger)
        {
            _gemini = gemini;
            _screenCapture = screenCapture;
            _speech = speech;
            _creds = creds;
            _logger = logger;

            InitializeComponent();
            
            // Start with semi-transparent
            this.Opacity = 0.95;
            OpacitySlider.Value = 0.95;
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

                var apiKey = _creds.GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ResponseText.Text = "❌ No API key configured.";
                    return;
                }

                var settings = new Models.AppSettings();
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
        }

        private async void ScreenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResponseText.Text = "📷 Capturing screen...";

                var screenshot = await _screenCapture.CaptureFullScreenAsync();
                var base64 = _screenCapture.BitmapToBase64(screenshot);
                screenshot.Dispose();

                var apiKey = _creds.GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ResponseText.Text = "❌ No API key configured.";
                    return;
                }

                ResponseText.Text = "🤖 Analyzing screen...";

                var settings = new Models.AppSettings();
                var response = await _gemini.SendVisionMessageAsync(
                    "Describe what you see in this screenshot. What applications or content are visible?",
                    base64,
                    apiKey,
                    settings);

                ResponseText.Text = $"Screen Analysis:\n\n{response}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Screen capture failed");
                ResponseText.Text = $"❌ Error: {ex.Message}";
            }
        }
    }
}
