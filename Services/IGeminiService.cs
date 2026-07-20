using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assistant.Models;

namespace Assistant.Services
{
    public interface IGeminiService
    {
        /// <summary>Sends a full conversation and returns the complete assistant reply.</summary>
        Task<string> SendMessageAsync(
            List<ChatMessage> history,
            string            apiKey,
            AppSettings       settings,
            CancellationToken ct = default);

        /// <summary>
        /// Sends a full conversation and yields text chunks as they arrive
        /// via Server-Sent Events (SSE).  The caller owns the CancellationToken.
        /// </summary>
        IAsyncEnumerable<string> SendMessageStreamAsync(
            List<ChatMessage> history,
            string            apiKey,
            AppSettings       settings,
            CancellationToken ct = default);

        /// <summary>
        /// Sends text + image to Gemini Vision API.
        /// </summary>
        Task<string> SendVisionMessageAsync(
            string            prompt,
            string            base64Image,
            string            apiKey,
            AppSettings       settings,
            CancellationToken ct = default);

        /// <summary>
        /// Makes a minimal test call to check whether the key is accepted by the API.
        /// Does NOT throw — returns false on any failure.
        /// </summary>
        Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default);
    }
}
