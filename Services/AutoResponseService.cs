using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Auto-response engine for interview scenarios.
    /// 
    /// Features:
    ///   1. Detects interview questions (patterns, keywords)
    ///   2. Combines screen context + audio transcription + detected question
    ///   3. Auto-generates response using Gemini
    ///   4. Optionally speaks response via TTS
    ///   5. Maintains response history for manual review
    /// 
    /// Workflow:
    ///   1. User enters interview (Zoom/Discord/GMeet)
    ///   2. Enable "Auto-Respond" in settings
    ///   3. System monitors audio + screen
    ///   4. When question detected → capture screen + audio
    ///   5. Send to Gemini: "I see this on screen [screenshot]. Person asked: [question]. Answer it."
    ///   6. Display response to user (optional auto-type into chat)
    /// </summary>
    public sealed class AutoResponseService : IAutoResponseService
    {
        private readonly IGeminiService _gemini;
        private readonly IScreenCaptureService _screenCapture;
        private readonly ILogger<AutoResponseService> _logger;

        private List<AutoResponseRule> _rules = new();
        private bool _isEnabled;

        public event EventHandler<AutoResponseEventArgs>? ResponseGenerated;
        public event EventHandler<string>? Error;

        public AutoResponseService(
            IGeminiService gemini,
            IScreenCaptureService screenCapture,
            ILogger<AutoResponseService> logger)
        {
            _gemini = gemini;
            _screenCapture = screenCapture;
            _logger = logger;
        }

        public void Enable()
        {
            _isEnabled = true;
            _logger.LogInformation("Auto-response engine enabled");
        }

        public void Disable()
        {
            _isEnabled = false;
            _logger.LogInformation("Auto-response engine disabled");
        }

        public bool IsEnabled => _isEnabled;

        public void AddRule(AutoResponseRule rule)
        {
            if (rule != null && !_rules.Any(r => r.Id == rule.Id))
            {
                _rules.Add(rule);
                _logger.LogInformation("Auto-response rule added: {Name} (pattern: {Pattern})", 
                    rule.Name, rule.Pattern);
            }
        }

        public void RemoveRule(string ruleId)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                _rules.Remove(rule);
                _logger.LogInformation("Auto-response rule removed: {Name}", rule.Name);
            }
        }

        public IEnumerable<AutoResponseRule> GetRules() => _rules.AsReadOnly();

        public void SetRules(List<AutoResponseRule> rules)
        {
            _rules = rules ?? new();
            _logger.LogInformation("Auto-response rules updated: {Count} rules", _rules.Count);
        }

        /// <summary>
        /// Detects if text matches any auto-response rules.
        /// Returns the matching rule, or null if no match.
        /// </summary>
        public AutoResponseRule? DetectQuestion(string text)
        {
            if (!_isEnabled || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            foreach (var rule in _rules)
            {
                if (MatchesPattern(text, rule.Pattern, rule.IsCaseSensitive))
                {
                    _logger.LogInformation("Question detected by rule: {Name}", rule.Name);
                    return rule;
                }
            }

            return null;
        }

        /// <summary>
        /// Generates auto-response for detected question.
        /// 
        /// Process:
        ///   1. Capture current screen
        ///   2. Build context: "Visual context: [screenshot]. Question: {question}"
        ///   3. Send to Gemini with system prompt
        ///   4. Get response
        ///   5. Fire ResponseGenerated event with response + screenshot + question
        /// </summary>
        public async Task<string> GenerateResponseAsync(
            string question,
            string? screenshotBase64,
            string apiKey,
            AppSettings settings,
            string? additionalContext = null,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(question))
                {
                    _logger.LogWarning("No question provided for response generation");
                    return string.Empty;
                }

                _logger.LogInformation("Generating auto-response for question: {Chars} chars", 
                    question.Length);

                // Capture screenshot if not provided
                if (string.IsNullOrEmpty(screenshotBase64))
                {
                    _logger.LogDebug("Capturing screenshot for context");
                    var bitmap = await _screenCapture.CaptureFullScreenAsync().ConfigureAwait(false);
                    screenshotBase64 = _screenCapture.BitmapToBase64(bitmap);
                    bitmap.Dispose();
                }

                // Build prompt with context
                var prompt = BuildPrompt(question, additionalContext);

                // Create request to Gemini
                var history = new List<ChatMessage>();
                var geminiSettings = new AppSettings
                {
                    SelectedModel = settings.SelectedModel,
                    Temperature = 0.5,  // Balanced creativity/accuracy
                    MaxOutputTokens = 2048,
                    SystemPrompt = GetAutoResponseSystemPrompt()
                };

                string response;

                if (!string.IsNullOrEmpty(screenshotBase64))
                {
                    // Include screenshot for visual context
                    response = await _gemini.SendVisionMessageAsync(
                        prompt,
                        screenshotBase64,
                        apiKey,
                        geminiSettings,
                        ct
                    ).ConfigureAwait(false);
                }
                else
                {
                    // Text-only response
                    response = await _gemini.SendMessageAsync(
                        history,
                        apiKey,
                        geminiSettings,
                        ct
                    ).ConfigureAwait(false);
                }

                _logger.LogInformation("Auto-response generated: {Chars} characters", response.Length);

                // Fire event for UI to display
                ResponseGenerated?.Invoke(this, new AutoResponseEventArgs
                {
                    Question = question,
                    Response = response,
                    ScreenshotBase64 = screenshotBase64,
                    Timestamp = DateTime.Now
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate auto-response");
                Error?.Invoke(this, ex.Message);
                return string.Empty;
            }
        }

        private string BuildPrompt(string question, string? context)
        {
            var prompt = $"I am in an interview and received this question:\n\n\"{question}\"";

            if (!string.IsNullOrEmpty(context))
            {
                prompt += $"\n\nAdditional context: {context}";
            }

            prompt += "\n\nProvide a professional, concise, and helpful response. " +
                     "Keep it conversational and avoid sounding scripted.";

            return prompt;
        }

        private string GetAutoResponseSystemPrompt()
        {
            return "You are an AI interview assistant. Your role is to help the user respond to interview " +
                   "questions by providing accurate, professional, and thoughtful answers. " +
                   "Be concise (2-3 sentences for most questions), friendly, and avoid overly formal language. " +
                   "If the question is unclear, ask for clarification. If you don't know the answer, suggest " +
                   "researching or provide a thoughtful generic response.";
        }

        private bool MatchesPattern(string text, string pattern, bool caseSensitive)
        {
            try
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.IsMatch(text, pattern, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid regex pattern: {Pattern}", pattern);
                // Fallback to simple substring matching
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return text.Contains(pattern, comparison);
            }
        }
    }

    /// <summary>
    /// Event arguments for auto-response generation.
    /// </summary>
    public class AutoResponseEventArgs : EventArgs
    {
        public string Question { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string? ScreenshotBase64 { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Auto-response rule for question detection.
    /// </summary>
    public class AutoResponseRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;  // Regex pattern to match
        public bool IsCaseSensitive { get; set; } = false;
        public bool IsEnabled { get; set; } = true;

        public override string ToString() => Name;
    }
}
