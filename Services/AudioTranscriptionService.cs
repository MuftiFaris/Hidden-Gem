using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Assistant.Models;
using Microsoft.Extensions.Logging;

namespace Assistant.Services
{
    /// <summary>
    /// Converts audio samples to text using Gemini API.
    /// 
    /// Strategy:
    ///   1. Collect audio frames from IAudioCaptureService or ISpeechService
    ///   2. Convert to WAV format (required by Google Speech-to-Text)
    ///   3. Encode as base64
    ///   4. Send to Gemini vision API with prompt "Transcribe the audio"
    ///   5. Extract text response
    /// 
    /// Alternative: Use Google Cloud Speech-to-Text API for better accuracy
    /// (but requires separate API key + service account setup)
    /// </summary>
    public sealed class AudioTranscriptionService : IAudioTranscriptionService
    {
        private readonly IGeminiService _gemini;
        private readonly ILogger<AudioTranscriptionService> _logger;

        public AudioTranscriptionService(IGeminiService gemini, ILogger<AudioTranscriptionService> logger)
        {
            _gemini = gemini;
            _logger = logger;
        }

        /// <summary>
        /// Transcribes audio samples to text using Gemini API.
        /// 
        /// Process:
        ///   1. Convert float samples to 16-bit PCM WAV format
        ///   2. Encode WAV as base64
        ///   3. Send to Gemini with prompt: "Transcribe this audio. Return only the transcription."
        ///   4. Extract text from response
        /// </summary>
        public async Task<string> TranscribeAudioAsync(
            float[] samples,
            string apiKey,
            int sampleRate = 16000,
            CancellationToken ct = default)
        {
            try
            {
                if (samples == null || samples.Length == 0)
                {
                    _logger.LogWarning("No audio samples provided");
                    return string.Empty;
                }

                _logger.LogInformation("Transcribing {Samples} audio samples at {Rate}Hz", 
                    samples.Length, sampleRate);

                // Convert float samples to WAV format
                var wavBytes = ConvertToWav(samples, sampleRate);
                var base64Audio = Convert.ToBase64String(wavBytes);

                _logger.LogDebug("Encoded audio: {Bytes} bytes → {Base64} base64", 
                    wavBytes.Length, base64Audio.Length);

                // Send to Gemini API
                var prompt = "Transcribe this audio. Return ONLY the transcription text, nothing else. " +
                            "If you cannot understand the audio, return [INAUDIBLE].";

                // Build a minimal request for audio analysis
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new { text = prompt },
                                new { inlineData = new { mimeType = "audio/wav", data = base64Audio } }
                            }
                        }
                    }
                };

                // Send to Gemini
                var history = new List<ChatMessage>();  // Empty history for one-off transcription
                var result = await _gemini.SendMessageAsync(history, apiKey, new Models.AppSettings
                {
                    SelectedModel = "gemini-3.5-flash",
                    Temperature = 0.3,  // Lower temperature for accurate transcription
                    MaxOutputTokens = 1024
                }, ct).ConfigureAwait(false);

                _logger.LogInformation("Transcription received: {Length} characters", result.Length);
                return result.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio transcription failed");
                return string.Empty;
            }
        }

        /// <summary>
        /// Converts float audio samples [-1, 1] to WAV format (16-bit PCM).
        /// WAV structure: RIFF header + fmt chunk + data chunk.
        /// </summary>
        private byte[] ConvertToWav(float[] samples, int sampleRate)
        {
            const ushort channels = 1;  // Mono
            const ushort bitsPerSample = 16;
            const ushort blockAlign = channels * bitsPerSample / 8;

            int audioDataSize = samples.Length * blockAlign;
            int fileSize = 36 + audioDataSize;  // RIFF header size + audio data

            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // RIFF header
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(fileSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                // fmt subchunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);  // Subchunk1Size (PCM = 16)
                writer.Write((ushort)1);  // AudioFormat (1 = PCM)
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * blockAlign);  // ByteRate
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                // data subchunk
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(audioDataSize);

                // Convert float samples to 16-bit PCM
                foreach (var sample in samples)
                {
                    // Clamp to [-1, 1]
                    var clamped = Math.Max(-1f, Math.Min(1f, sample));
                    // Convert to 16-bit signed int
                    short pcm = (short)(clamped * 32767);
                    writer.Write(pcm);
                }

                return ms.ToArray();
            }
        }
    }
}
