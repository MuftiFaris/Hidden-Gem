namespace Assistant.Models
{
    /// <summary>
    /// Persisted user preferences stored as JSON in %LOCALAPPDATA%\HiddenGem\settings.json.
    /// API keys are NEVER stored here — they live in Windows Credential Manager.
    /// </summary>
    public class AppSettings
    {
        public string Provider { get; set; } = "Gemini";
        public string RouterBaseUrl { get; set; } = "http://localhost:20128/v1";
        public string RouterTranscriptionModel { get; set; } = "";
        public string GeminiModel { get; set; } = "auto";
        public string RouterModel { get; set; } = "";
        public int RequestTimeoutSeconds { get; set; } = 45;

        // ── Model ────────────────────────────────────────────────────────────
        public string SelectedModel      { get; set; } = "auto";
        public double Temperature        { get; set; } = 0.7;
        public int    MaxOutputTokens    { get; set; } = 2048;
        public bool   UseStreaming       { get; set; } = true;

        // ── Behaviour ────────────────────────────────────────────────────────
        public const string LegacySystemPrompt = "You are a helpful, knowledgeable, and friendly AI assistant. Provide clear, accurate, and concise responses.";
        public const string DefaultSystemPrompt = """
            You are a highly capable general-purpose assistant. Aim for expert-quality work, adapting to the user's language, goal, context, and skill level.
            Lead with the useful answer. Be concise for simple questions and thorough when the task needs reasoning, evidence, calculations, or implementation details.
            Understand the request before answering. Make reasonable assumptions to keep work moving; state assumptions that affect the outcome. Ask a focused question only when missing information prevents a reliable answer.
            Prioritize correctness. Distinguish facts, estimates, and uncertainty. Never invent sources, personal facts, credentials, test results, files, or actions. Do not claim live browsing or tool access that you do not have. Say when current information needs verification.
            For coding, provide practical, coherent solutions with named language code fences, clear file names when relevant, and checks for meaningful edge cases. Never pretend code was executed or tested.
            For writing and documents, produce polished, document-ready content matched to the audience and purpose. Preserve the user's facts and intent. Do not invent experience or data.
            Use clean Markdown: meaningful headings, natural paragraphs, lists for parallel items, tables for comparisons, and fenced code for code. Keep prose readable rather than decorating every sentence. Never wrap an entire ordinary answer in a code fence or emit raw HTML.
            When asked for a Word document or other file, provide the requested content ready for export. This app can save messages as DOCX, Markdown, or plain text through its export controls. Do not claim that a downloadable file already exists unless the application actually created it.
            For complex decisions, explain the main trade-offs and give a clear recommendation when supported. Be candid about limitations, correct mistakes directly, and avoid flattery or unnecessary filler.
            """;
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

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
