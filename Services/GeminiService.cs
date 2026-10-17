using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services;

public sealed class GeminiService : IGeminiService, IDisposable
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private readonly HttpClient _http;
    private readonly OpenAiCompatibleService _router;
    private readonly ILogger<GeminiService> _logger;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private readonly SemaphoreSlim _modelGate = new(1, 1);
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly Dictionary<string, DateTime> _unavailable = new();
    private string _modelKeyHash = "";
    private IReadOnlyList<string> _models = Array.Empty<string>();
    private DateTime _modelExpiry;
    private DateTime _lastRequest;

    public GeminiService(ILogger<GeminiService> logger)
    {
        _logger = logger;
        _router = new OpenAiCompatibleService(logger);
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(string apiKey, CancellationToken ct = default, AppSettings? settings = null)
    {
        if (settings?.Provider == "9Router") return await _router.GetModelsAsync(apiKey, settings, ct).ConfigureAwait(false);
        var hash = KeyHash(apiKey);
        await _modelGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_modelKeyHash == hash && DateTime.UtcNow < _modelExpiry) return _models;
            var models = new List<string>();
            string? pageToken = null;
            do
            {
                var url = $"{BaseUrl}?pageSize=100&key={Uri.EscapeDataString(apiKey.Trim())}";
                if (pageToken != null) url += "&pageToken=" + Uri.EscapeDataString(pageToken);
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
                var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                EnsureSuccess(response, raw);
                using var document = JsonDocument.Parse(raw);
                if (document.RootElement.TryGetProperty("models", out var items))
                foreach (var model in items.EnumerateArray())
                {
                    if (!model.TryGetProperty("supportedGenerationMethods", out var methods) ||
                        !methods.EnumerateArray().Any(x => x.GetString() == "generateContent")) continue;
                    var name = model.GetProperty("name").GetString()!.Replace("models/", "");
                    if (!new[] { "image", "tts", "live", "transcribe", "embedding", "robotics" }.Any(name.Contains)) models.Add(name);
                }
                pageToken = document.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            } while (!string.IsNullOrEmpty(pageToken));
            _models = models.Distinct().ToList();
            _modelKeyHash = hash;
            _modelExpiry = DateTime.UtcNow.AddMinutes(5);
            return _models;
        }
        finally { _modelGate.Release(); }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
        => (await GetAvailableModelsAsync(apiKey, ct).ConfigureAwait(false)).Count > 0;

    public Task<string> TestConnectionAsync(string apiKey, AppSettings settings, CancellationToken ct = default)
        => settings.Provider == "9Router" ? _router.TestAsync(apiKey, settings, ct) : RequestBudget.RunAsync(token => TestConnectionCoreAsync(apiKey, settings, token), settings, ct);

    private async Task<string> TestConnectionCoreAsync(string apiKey, AppSettings settings, CancellationToken ct)
    {
        var request = new GeminiRequest
        {
            Contents = new() { new() { Role = "user", Parts = new() { new() { Text = "Reply with only OK." } } } },
            GenerationConfig = new() { Temperature = 0, MaxOutputTokens = 512 }
        };
        var (response, model) = await SendRequestAsync(request, apiKey, settings, false, ct).ConfigureAwait(false);
        using (response)
        {
            var result = ReadText(JsonSerializer.Deserialize<GeminiResponse>(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false), _json));
            if (string.IsNullOrWhiteSpace(result)) throw new GeminiApiException("The model returned no text. Try Auto (Flash) or a higher token limit.", 200);
        }
        return model;
    }

    public async Task<string> SendMessageAsync(List<ChatMessage> history, string apiKey, AppSettings settings, CancellationToken ct = default)
        => await (settings.Provider == "9Router" ? _router.SendMessageAsync(history, apiKey, settings, ct) : RequestBudget.RunAsync(token => SendNonStreamingAsync(BuildRequest(history, settings), apiKey, settings, token), settings, ct)).ConfigureAwait(false);

    public Task<string> SendVisionMessageAsync(string prompt, string base64Image, string apiKey, AppSettings settings, CancellationToken ct = default)
        => settings.Provider == "9Router" ? _router.SendVisionAsync(prompt, base64Image, apiKey, settings, ct) : RequestBudget.RunAsync(token => SendNonStreamingAsync(BuildMediaRequest(prompt, base64Image, "image/jpeg", settings), apiKey, settings, token), settings, ct);

    public Task<string> SendAudioMessageAsync(byte[] wav, string apiKey, AppSettings settings, CancellationToken ct = default)
        => settings.Provider == "9Router" ? _router.TranscribeAsync(wav, apiKey, settings, ct) : RequestBudget.RunAsync(token => SendNonStreamingAsync(BuildMediaRequest("Transcribe the speech in its original language. Return only the transcript, or [INAUDIBLE] when there is no intelligible speech.", Convert.ToBase64String(wav), "audio/wav", settings), apiKey, settings, token), settings, ct);

    private async Task<string> SendNonStreamingAsync(GeminiRequest request, string apiKey, AppSettings settings, CancellationToken ct)
    {
        var (response, _) = await SendRequestAsync(request, apiKey, settings, false, ct).ConfigureAwait(false);
        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var result = ReadText(JsonSerializer.Deserialize<GeminiResponse>(raw, _json));
            if (string.IsNullOrWhiteSpace(result)) throw new GeminiApiException("Gemini returned no text. The response may be blocked or its token budget exhausted. Increase max output tokens or try another model.", 200);
            return result;
        }
    }

    public IAsyncEnumerable<string> SendMessageStreamAsync(List<ChatMessage> history, string apiKey, AppSettings settings, CancellationToken ct = default)
        => settings.Provider == "9Router" ? _router.SendStreamAsync(history, apiKey, settings, ct) : RequestBudget.StreamAsync(token => StreamCoreAsync(history, apiKey, settings, token), settings, ct);

    private async IAsyncEnumerable<string> StreamCoreAsync(List<ChatMessage> history, string apiKey, AppSettings settings, [EnumeratorCancellation] CancellationToken ct)
    {
        var (response, _) = await SendRequestAsync(BuildRequest(history, settings), apiKey, settings, true, ct).ConfigureAwait(false);
        using (response)
        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), Encoding.UTF8))
        {
            bool receivedText = false;
            while (true)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line[5..].TrimStart();
                if (data == "[DONE]") break;
                var chunk = JsonSerializer.Deserialize<GeminiResponse>(data, _json);
                if (chunk?.Error != null) throw new GeminiApiException(chunk.Error.Message, chunk.Error.Code);
                var text = ReadText(chunk);
                if (text.Length > 0) { receivedText = true; yield return text; }
            }
            ct.ThrowIfCancellationRequested();
            if (!receivedText) throw new GeminiApiException("Gemini returned no text. Increase max output tokens or try Auto (Flash).", 200);
        }
    }

    private async Task<(HttpResponseMessage Response, string Model)> SendRequestAsync(GeminiRequest request, string apiKey, AppSettings settings, bool stream, CancellationToken ct)
    {
        var models = await GetAvailableModelsAsync(apiKey, ct).ConfigureAwait(false);
        var selected = (settings.SelectedModel ?? "auto").Trim().Replace("models/", "");
        bool automatic = selected.Length == 0 || selected == "auto";
        var candidates = automatic
            ? models.Where(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Contains("preview") || m.Contains("experimental"))
                .ThenByDescending(ModelVersion).ThenBy(m => m.Contains("lite") ? 0 : 1).ToList()
            : new List<string> { selected };
        if (!automatic && !models.Contains(selected)) throw new GeminiApiException($"Model '{selected}' is no longer available. Select Auto (Flash) in Settings and save.", 404);
        if (candidates.Count == 0) throw new GeminiApiException("No general-purpose Flash model is available for this API key. Check the API project in Google AI Studio.", 404);
        var keyHash = KeyHash(apiKey);
        GeminiApiException? lastFailure = null;
        foreach (var model in candidates.Take(3))
        {
            lock (_unavailable)
                if (automatic && _unavailable.TryGetValue(keyHash + model, out var until) && until > DateTime.UtcNow) continue;
            await _requestGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var remaining = 1000 - (DateTime.UtcNow - _lastRequest).TotalMilliseconds;
                if (remaining > 0) await Task.Delay((int)remaining, ct).ConfigureAwait(false);
                _lastRequest = DateTime.UtcNow;
            }
            finally { _requestGate.Release(); }
            var endpoint = stream ? "streamGenerateContent?alt=sse&" : "generateContent?";
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{Uri.EscapeDataString(model)}:{endpoint}key={Uri.EscapeDataString(apiKey.Trim())}")
            { Content = new StringContent(JsonSerializer.Serialize(request, _json), Encoding.UTF8, "application/json") };
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(message, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) { throw new GeminiApiException("Cannot reach Gemini. Check your internet connection and try again.", 0); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new GeminiApiException("Gemini request timed out. Try again or choose a faster model.", 0); }
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Gemini request accepted with model {Model}", model);
                return (response, model);
            }
            try
            {
                EnsureSuccess(response, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false), model);
            }
            catch (GeminiApiException ex) when (automatic && ex.IsQuotaUnavailable)
            {
                lastFailure = ex;
                lock (_unavailable) _unavailable[keyHash + model] = DateTime.UtcNow.AddMinutes(5);
                _logger.LogWarning("Auto selection skipped zero-quota model {Model}", model);
            }
            finally { response.Dispose(); }
        }
        throw lastFailure ?? new GeminiApiException("The available Auto models have no quota for this API project. Check usage/billing in Google AI Studio or select another model and test it.", 429, true);
    }

    private static Version ModelVersion(string model)
    {
        var match = Regex.Match(model, @"gemini-(\d+)\.(\d+)");
        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)) : new Version(0, 0);
    }

    private static string KeyHash(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new GeminiApiException("Save a Gemini API key in Settings first.", 401);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey.Trim())));
    }

    private void EnsureSuccess(HttpResponseMessage response, string raw, string? model = null)
    {
        if (response.IsSuccessStatusCode) return;
        GeminiError? error = null;
        try { error = JsonSerializer.Deserialize<GeminiResponse>(raw, _json)?.Error; } catch (JsonException) { }
        var provider = error?.Message ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        bool zeroQuota = (int)response.StatusCode == 429 && Regex.IsMatch(provider, @"limit:\s*0(?:\D|$)", RegexOptions.IgnoreCase);
        var text = zeroQuota
            ? $"Your API project has zero quota for {model ?? "this model"}. Choose Auto (Flash) in Settings and test the connection, or check this project's API billing/limits in Google AI Studio. Waiting will not enable a zero-quota model."
            : (int)response.StatusCode == 429
                ? "Gemini API quota/rate limit reached. Check this project's usage limits in Google AI Studio and retry after the limit resets."
                : provider;
        _logger.LogWarning("Gemini request failed with HTTP {Status}: {Detail}", (int)response.StatusCode, provider);
        throw new GeminiApiException(text, (int)response.StatusCode, zeroQuota, provider);
    }

    private static string ReadText(GeminiResponse? response)
        => string.Concat(response?.Candidates?.FirstOrDefault()?.Content?.Parts?.Where(p => !p.Thought).Select(p => p.Text ?? "") ?? Enumerable.Empty<string>());
    private static GeminiRequest BuildRequest(List<ChatMessage> history, AppSettings settings)
        => new()
        {
            Contents = history.Where(m => m.Role != MessageRole.System && !m.IsError && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => new GeminiContent { Role = m.Role == MessageRole.User ? "user" : "model", Parts = new() { new() { Text = m.Content } } }).ToList(),
            SystemInstruction = string.IsNullOrWhiteSpace(settings.SystemPrompt) ? null : new() { Parts = new() { new() { Text = settings.SystemPrompt } } },
            GenerationConfig = new() { Temperature = settings.Temperature, MaxOutputTokens = settings.MaxOutputTokens }
        };
    private static GeminiRequest BuildMediaRequest(string prompt, string data, string mime, AppSettings settings)
    {
        var request = BuildRequest(new(), settings);
        request.Contents.Add(new() { Role = "user", Parts = new() { new() { Text = prompt }, new() { InlineData = new() { MimeType = mime, Data = data } } } });
        return request;
    }
    public void Dispose() { _router.Dispose(); _http.Dispose(); _modelGate.Dispose(); _requestGate.Dispose(); }
}

public sealed class GeminiApiException : Exception
{
    public int HttpStatusCode { get; }
    public bool IsQuotaUnavailable { get; }
    public string ProviderMessage { get; }
    public GeminiApiException(string message, int httpStatusCode, bool isQuotaUnavailable = false, string? providerMessage = null) : base(message)
    { HttpStatusCode = httpStatusCode; IsQuotaUnavailable = isQuotaUnavailable; ProviderMessage = providerMessage ?? message; }
}
