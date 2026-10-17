using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services;

/// <summary>OpenAI-compatible 9Router adapter. Routes/models determine supported modalities.</summary>
public sealed class OpenAiCompatibleService : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    public OpenAiCompatibleService(ILogger logger, HttpClient? http = null)
    {
        _logger = logger;
        _http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    }
    public static string NormalizeBaseUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new GeminiApiException("Enter the 9Router API base URL, such as http://localhost:20128/v1. Use HTTPS for remote servers.", 400);
        return uri.AbsoluteUri.TrimEnd('/');
    }
    private HttpRequestMessage Message(HttpMethod method, string route, string key, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new GeminiApiException("Save your 9Router dashboard API key in Settings first.", 401);
        var message = new HttpRequestMessage(method, NormalizeBaseUrl(settings.RouterBaseUrl) + route);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        return message;
    }
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool stream, CancellationToken ct)
    {
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { throw new GeminiApiException("Cannot reach 9Router. Keep its CMD/server running and check the base URL and port.", 0); }
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            string detail = response.ReasonPhrase ?? "Request failed";
            try
            {
                using var json = JsonDocument.Parse(raw);
                if (json.RootElement.TryGetProperty("error", out var error))
                    detail = error.ValueKind == JsonValueKind.String ? error.GetString()! : error.TryGetProperty("message", out var text) ? text.GetString()! : detail;
            } catch (JsonException) { }
            _logger.LogWarning("9Router HTTP {Code}: {Detail}", (int)response.StatusCode, detail);
            throw new GeminiApiException($"9Router HTTP {(int)response.StatusCode}: {detail}", (int)response.StatusCode);
        }
    }
    public Task<IReadOnlyList<string>> GetModelsAsync(string key, AppSettings settings, CancellationToken ct)
        => RequestBudget.RunAsync<IReadOnlyList<string>>(async token =>
        {
            using var request = Message(HttpMethod.Get, "/models", key, settings);
            using var response = await SendAsync(request, false, token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            if (!json.RootElement.TryGetProperty("data", out var data)) throw new GeminiApiException("9Router returned no model list. Check that the base URL ends in /v1.", 200);
            return data.EnumerateArray().Select(m => m.GetProperty("id").GetString()!).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().OrderBy(m => m).ToList();
        }, settings, ct);
    private static string Model(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SelectedModel) || settings.SelectedModel == "auto")
            throw new GeminiApiException("For 9Router, choose or paste the exact model ID from its dashboard. Auto (Flash) is for direct Gemini only.", 400);
        return settings.SelectedModel.Trim();
    }
    private static List<object> Messages(List<ChatMessage> history, AppSettings settings)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(settings.SystemPrompt)) messages.Add(new { role = "system", content = settings.SystemPrompt });
        messages.AddRange(history.Where(m => m.Role != MessageRole.System && !m.IsError && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => (object)new { role = m.Role == MessageRole.User ? "user" : "assistant", content = m.Content }));
        return messages;
    }
    private HttpRequestMessage ChatRequest(List<object> messages, string key, AppSettings settings, bool stream)
    {
        var request = Message(HttpMethod.Post, "/chat/completions", key, settings);
        request.Content = new StringContent(JsonSerializer.Serialize(new { model = Model(settings), messages, temperature = settings.Temperature, max_tokens = settings.MaxOutputTokens, stream }), Encoding.UTF8, "application/json");
        return request;
    }
    private static string Text(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String) return content.GetString() ?? "";
        if (content.ValueKind == JsonValueKind.Array) return string.Concat(content.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
        return "";
    }
    private Task<string> CompleteAsync(List<object> messages, string key, AppSettings settings, CancellationToken ct)
        => RequestBudget.RunAsync(async token =>
        {
            using var request = ChatRequest(messages, key, settings, false);
            using var response = await SendAsync(request, false, token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            string result = "";
            if (json.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 && choices[0].GetProperty("message").TryGetProperty("content", out var content)) result = Text(content);
            if (string.IsNullOrWhiteSpace(result)) throw new GeminiApiException("9Router returned no answer text. Try another route/model or increase the output budget.", 200);
            return result;
        }, settings, ct);
    public Task<string> SendMessageAsync(List<ChatMessage> history, string key, AppSettings settings, CancellationToken ct)
        => CompleteAsync(Messages(history, settings), key, settings, ct);
    public Task<string> SendVisionAsync(string prompt, string image, string key, AppSettings settings, CancellationToken ct)
    {
        var messages = Messages(new(), settings);
        messages.Add(new { role = "user", content = new object[] { new { type = "text", text = prompt }, new { type = "image_url", image_url = new { url = "data:image/jpeg;base64," + image } } } });
        return CompleteAsync(messages, key, settings, ct);
    }
    public async Task<string> TestAsync(string key, AppSettings settings, CancellationToken ct)
    {
        await SendMessageAsync(new() { new() { Role = MessageRole.User, Content = "Reply with only OK." } }, key, settings, ct).ConfigureAwait(false);
        return Model(settings);
    }
    public IAsyncEnumerable<string> SendStreamAsync(List<ChatMessage> history, string key, AppSettings settings, CancellationToken ct)
        => RequestBudget.StreamAsync(token => StreamCoreAsync(history, key, settings, token), settings, ct);
    private async IAsyncEnumerable<string> StreamCoreAsync(List<ChatMessage> history, string key, AppSettings settings, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request = ChatRequest(Messages(history, settings), key, settings, true);
        using var response = await SendAsync(request, true, ct).ConfigureAwait(false);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), Encoding.UTF8);
        bool received = false;
        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line[5..].TrimStart(); if (data == "[DONE]") break;
            using var json = JsonDocument.Parse(data);
            if (json.RootElement.TryGetProperty("error", out var error)) throw new GeminiApiException("9Router stream failed: " + error, 502);
            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) continue;
            if (!choices[0].TryGetProperty("delta", out var delta) || !delta.TryGetProperty("content", out var content)) continue;
            var text = Text(content);
            if (text.Length > 0) { received = true; yield return text; }
        }
        ct.ThrowIfCancellationRequested();
        if (!received) throw new GeminiApiException("9Router stream ended without text. Try another route/model.", 200);
    }
    public Task<string> TranscribeAsync(byte[] wav, string key, AppSettings settings, CancellationToken ct)
        => RequestBudget.RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(settings.RouterTranscriptionModel)) throw new GeminiApiException("9Router voice requires a configured speech-to-text model. Enter its exact STT model ID in Settings, or use direct Gemini for audio.", 400);
            using var request = Message(HttpMethod.Post, "/audio/transcriptions", key, settings);
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(settings.RouterTranscriptionModel.Trim()), "model");
            var file = new ByteArrayContent(wav); file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(file, "file", "speech.wav");
            request.Content = form;
            using var response = await SendAsync(request, false, token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            if (!json.RootElement.TryGetProperty("text", out var text)) throw new GeminiApiException("9Router returned no transcription. Check the configured STT route/model.", 200);
            return text.GetString() ?? "";
        }, settings, ct);
    public void Dispose() => _http.Dispose();
}
