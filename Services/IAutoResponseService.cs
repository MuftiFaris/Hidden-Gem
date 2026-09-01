using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assistant.Models;

namespace Assistant.Services
{
    /// <summary>
    /// Auto-response engine for interview assistance.
    /// Detects questions, captures context (screen + audio), and generates responses.
    /// </summary>
    public interface IAutoResponseService
    {
        /// <summary>
        /// Fired when a response is successfully generated.
        /// UI can display the response to user for review/copy.
        /// </summary>
        event EventHandler<AutoResponseEventArgs>? ResponseGenerated;

        /// <summary>
        /// Fired when an error occurs during response generation.
        /// </summary>
        event EventHandler<string>? Error;

        /// <summary>
        /// Enables the auto-response engine.
        /// </summary>
        void Enable();

        /// <summary>
        /// Disables the auto-response engine.
        /// </summary>
        void Disable();

        /// <summary>
        /// Returns whether auto-response is currently enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Adds a rule for detecting interview questions.
        /// </summary>
        void AddRule(AutoResponseRule rule);

        /// <summary>
        /// Removes a rule by ID.
        /// </summary>
        void RemoveRule(string ruleId);

        /// <summary>
        /// Gets all configured rules.
        /// </summary>
        IEnumerable<AutoResponseRule> GetRules();

        /// <summary>
        /// Replaces all rules with new set.
        /// </summary>
        void SetRules(List<AutoResponseRule> rules);

        /// <summary>
        /// Detects if text matches any configured rule.
        /// Returns matching rule or null if no match.
        /// </summary>
        AutoResponseRule? DetectQuestion(string text);

        /// <summary>
        /// Generates response for detected question.
        /// Combines screen context + audio transcription to provide informed answer.
        /// 
        /// Parameters:
        ///   - question: Detected interview question
        ///   - screenshotBase64: Optional screenshot for visual context (captured if null)
        ///   - apiKey: Gemini API key
        ///   - settings: App settings (model, temperature, etc.)
        ///   - additionalContext: Optional context (e.g., transcribed audio from participant)
        ///   - ct: Cancellation token
        /// 
        /// Returns: Generated response text
        /// Fires: ResponseGenerated event for UI
        /// </summary>
        Task<string> GenerateResponseAsync(
            string question,
            string? screenshotBase64,
            string apiKey,
            AppSettings settings,
            string? additionalContext = null,
            CancellationToken ct = default);
    }
}
