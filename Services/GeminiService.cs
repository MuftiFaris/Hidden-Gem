using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Wraps the Google Gemini REST API.
    ///
    /// Endpoints used:
    ///   Non-streaming: POST /v1beta/models/{model}:generateContent?key={key}
    ///   Streaming:     POST /v1beta/models/{model}:streamGenerateContent?alt=sse&amp;key={key}
    ///
    /// The streaming endpoint returns Server-Sent Events (SSE).  Each "data:" line
    /// contains a partial GeminiResponse JSON object.  We accumulate the text chunks
    /// and yield them one by one so the UI can display progressive output.
    ///
    /// API reference: https://ai.google.dev/api/generate-content
    /// </summary>
    public sealed class GeminiService : IGeminiService, IDisposable
    {
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

        private readonly HttpClient            _http;
        private readonly ILogger<GeminiService> _logger;
        private readonly JsonSerializerOptions  _json;

        public GeminiService(ILogger<GeminiService> logger)
        {
            _logger = logger;
            _http   = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _json   = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive  = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        // ── Non-streaming ─────────────────────────────────────────────────────

        public async Task<string> SendMessageAsync(
            List<ChatMessage> history,
            string            apiKey,
            AppSettings       settings,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("API key is null or empty");
                throw new GeminiApiException("API key is required", 401);
            }

            var request = BuildRequest(history, settings);
            var url     = $"{BaseUrl}/{settings.SelectedModel}:generateContent?key={apiKey}";
            var body    = Serialize(request);

            _logger.LogInformation("Sending non-streaming request [Model: {Model}, Temp: {Temp}, MaxTokens: {MaxTokens}]", 
                settings.SelectedModel, settings.Temperature, settings.MaxOutputTokens);

            try
            {
                using var resp = await _http.PostAsync(url, body, ct).ConfigureAwait(false);
                var raw  = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                _logger.LogDebug("Response status: {StatusCode}", (int)resp.StatusCode);

                EnsureSuccess(resp, raw);

                var gemini = Deserialize<GeminiResponse>(raw);
                var result = gemini?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
                
                _logger.LogInformation("Response received: {Length} characters", result.Length);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during API request");
                throw new GeminiApiException($"Network error: {ex.Message}", 0);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Request timeout");
                throw new GeminiApiException("Request timeout - check network connection", 0);
            }
        }

        // ── Streaming (SSE) ────────────────────────────────────────────────────

        public async IAsyncEnumerable<string> SendMessageStreamAsync(
            List<ChatMessage> history,
            string            apiKey,
            AppSettings       settings,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("API key is null or empty");
                throw new GeminiApiException("API key is required", 401);
            }

            var request    = BuildRequest(history, settings);
            var url        = $"{BaseUrl}/{settings.SelectedModel}:streamGenerateContent?alt=sse&key={apiKey}";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = Serialize(request)
            };

            _logger.LogInformation("Sending streaming request [Model: {Model}, Temp: {Temp}, MaxTokens: {MaxTokens}]", 
                settings.SelectedModel, settings.Temperature, settings.MaxOutputTokens);

            HttpResponseMessage? resp = null;
            System.IO.StreamReader? reader = null;
            
            try
            {
                resp = await _http
                    .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                _logger.LogDebug("Stream response status: {StatusCode}", (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    EnsureSuccess(resp, raw);   // will throw
                }

                var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                reader = new System.IO.StreamReader(stream, Encoding.UTF8);

                int chunkCount = 0;
                while (!reader.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line is null) break;

                    // SSE lines that carry data are prefixed with "data: "
                    if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

                    var data = line["data: ".Length..];
                    if (data == "[DONE]")
                    {
                        _logger.LogInformation("Stream completed. Total chunks: {Count}", chunkCount);
                        break;
                    }

                    GeminiResponse? chunk = null;
                    try   { chunk = Deserialize<GeminiResponse>(data); }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Skipping unparseable SSE chunk: {Data}", data);
                        continue;
                    }

                    var text = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        chunkCount++;
                        yield return text;
                    }
                }
            }
            finally
            {
                reader?.Dispose();
                resp?.Dispose();
            }
        }

        // ── Validation ────────────────────────────────────────────────────────

        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Validating API key (length: {Length})", apiKey?.Length ?? 0);

                var req = new GeminiRequest
                {
                    Contents = new List<GeminiContent>
                    {
                        new() { Role = "user", Parts = new() { new() { Text = "Hi" } } }
                    },
                    GenerationConfig = new GenerationConfig { MaxOutputTokens = 5 }
                };

                var url  = $"{BaseUrl}/gemini-3.5-flash:generateContent?key={apiKey}";
                var body = Serialize(req);

                _logger.LogDebug("Validation URL: {Url}", url.Replace(apiKey ?? "", "***KEY***"));

                using var resp = await _http.PostAsync(url, body, ct).ConfigureAwait(false);
                var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                _logger.LogInformation("Validation response: {StatusCode}", (int)resp.StatusCode);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Validation failed: {StatusCode} - {Response}", 
                        (int)resp.StatusCode, raw);
                }

                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API key validation exception");
                return false;
            }
        }

        // ── Vision API ─────────────────────────────────────────────────────────

        public async Task<string> SendVisionMessageAsync(
            string            prompt,
            string            base64Image,
            string            apiKey,
            AppSettings       settings,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("API key is null or empty");
                throw new GeminiApiException("API key is required", 401);
            }

            // Use vision-capable model
            var model = "gemini-3.5-flash";
            var request = new GeminiRequest
            {
                Contents = new List<GeminiContent>
                {
                    new()
                    {
                        Role = "user",
                        Parts = new List<GeminiPart>
                        {
                            new() { Text = prompt },
                            new()
                            {
                                InlineData = new InlineData
                                {
                                    MimeType = "image/jpeg",
                                    Data = base64Image
                                }
                            }
                        }
                    }
                },
                GenerationConfig = new GenerationConfig
                {
                    Temperature = settings.Temperature,
                    MaxOutputTokens = settings.MaxOutputTokens
                }
            };

            var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";
            var body = Serialize(request);

            _logger.LogInformation("Sending vision request [Model: {Model}, ImageSize: {Size}KB]", 
                model, base64Image.Length / 1024);

            try
            {
                using var resp = await _http.PostAsync(url, body, ct).ConfigureAwait(false);
                var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                _logger.LogDebug("Vision response status: {StatusCode}", (int)resp.StatusCode);

                EnsureSuccess(resp, raw);

                var gemini = Deserialize<GeminiResponse>(raw);
                var result = gemini?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

                _logger.LogInformation("Vision response received: {Length} characters", result.Length);
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during vision API request");
                throw new GeminiApiException($"Network error: {ex.Message}", 0);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Vision request timeout");
                throw new GeminiApiException("Request timeout - check network connection", 0);
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static GeminiRequest BuildRequest(List<ChatMessage> history, AppSettings settings)
        {
            // Map user/assistant messages; exclude System-role messages from API payload
            // (those go into systemInstruction instead).
            var contents = history
                .Where(m => m.Role != MessageRole.System)
                .Select(m => new GeminiContent
                {
                    Role  = m.Role == MessageRole.User ? "user" : "model",
                    Parts = new List<GeminiPart> { new() { Text = m.Content } }
                })
                .ToList();

            GeminiContent? sysInstr = null;
            if (!string.IsNullOrWhiteSpace(settings.SystemPrompt))
            {
                sysInstr = new GeminiContent
                {
                    Parts = new List<GeminiPart> { new() { Text = settings.SystemPrompt } }
                };
            }

            return new GeminiRequest
            {
                Contents         = contents,
                SystemInstruction = sysInstr,
                GenerationConfig = new GenerationConfig
                {
                    Temperature    = settings.Temperature,
                    MaxOutputTokens = settings.MaxOutputTokens
                }
            };
        }

        private StringContent Serialize(object obj)
        {
            var json = JsonSerializer.Serialize(obj, _json);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private T? Deserialize<T>(string json)
            => JsonSerializer.Deserialize<T>(json, _json);

        private void EnsureSuccess(HttpResponseMessage resp, string raw)
        {
            if (resp.IsSuccessStatusCode) return;

            GeminiError? err = null;
            try
            {
                var parsed = Deserialize<GeminiResponse>(raw);
                err = parsed?.Error;
            }
            catch { /* ignore parse errors in error path */ }

            var msg = err?.Message ?? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
            
            // Improve rate limit error message
            if ((int)resp.StatusCode == 429)
            {
                msg = "⏱️ Rate limit exceeded. Free tier allows 15 requests/minute. Please wait 60 seconds and try again.";
            }
            
            _logger.LogError("Gemini API error {Code}: {Message}", (int)resp.StatusCode, msg);
            throw new GeminiApiException(msg, (int)resp.StatusCode);
        }

        public void Dispose() => _http.Dispose();
    }

    /// <summary>Strongly typed exception for Gemini REST API errors.</summary>
    public sealed class GeminiApiException : Exception
    {
        public int HttpStatusCode { get; }
        public GeminiApiException(string message, int httpStatusCode) : base(message)
            => HttpStatusCode = httpStatusCode;
    }
}
