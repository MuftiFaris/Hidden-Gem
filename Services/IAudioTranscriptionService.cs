using System.Threading;
using System.Threading.Tasks;

namespace Assistant.Services
{
    /// <summary>
    /// Transcribes audio samples to text using Gemini API.
    /// </summary>
    public interface IAudioTranscriptionService
    {
        /// <summary>
        /// Converts audio samples to text.
        /// 
        /// Parameters:
        ///   - samples: Float array [-1, 1] from audio capture
        ///   - apiKey: Gemini API key
        ///   - sampleRate: Audio sample rate (default 16000 Hz / 16 kHz)
        ///   - ct: Cancellation token
        /// 
        /// Returns: Transcribed text or empty string on error.
        /// </summary>
        Task<string> TranscribeAudioAsync(
            float[] samples,
            string apiKey,
            int sampleRate = 16000,
            CancellationToken ct = default);
    }
}
