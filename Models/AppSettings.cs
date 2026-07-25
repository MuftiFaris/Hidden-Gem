namespace Assistant.Models
{
    /// <summary>
    /// Persisted user preferences stored as JSON in %LOCALAPPDATA%\MicrosoftEdge\settings.json.
    /// API keys are NEVER stored here — they live in Windows Credential Manager.
    /// </summary>
    public class AppSettings
    {
        // ── Model ────────────────────────────────────────────────────────────
        public string SelectedModel      { get; set; } = "gemini-1.5-flash";
        public double Temperature        { get; set; } = 0.7;
        public int    MaxOutputTokens    { get; set; } = 2048;
        public bool   UseStreaming       { get; set; } = true;

        // ── Behaviour ────────────────────────────────────────────────────────
        public string SystemPrompt       { get; set; } =
            "You are a helpful, knowledgeable, and friendly AI assistant. " +
            "Provide clear, accurate, and concise responses.";

        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool StartMinimized        { get; set; } = false;

        // ── Privacy ──────────────────────────────────────────────────────────
        /// <summary>
        /// When true the app calls SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)
        /// to request that the OS hide window content from screen-capture APIs.
        /// See WindowPrivacyHelper for full limitation notes.
        /// </summary>
        public bool PrivacyModeEnabled   { get; set; } = false;

        // ── Logging ──────────────────────────────────────────────────────────
        public bool EnableLogging        { get; set; } = true;
        /// <summary>
        /// Conversation logging is OFF by default to protect user privacy.
        /// Users must explicitly opt in.
        /// </summary>
        public bool LogConversations     { get; set; } = false;
    }
}
