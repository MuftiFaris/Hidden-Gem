using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assistant.Services
{
    public interface ISpeechService
    {
        /// <summary>Starts listening to microphone input.</summary>
        Task<string> RecognizeSpeechAsync(CancellationToken ct = default);

        /// <summary>Checks if speech recognition is available on the system.</summary>
        bool IsAvailable();

        /// <summary>Gets list of available input devices.</summary>
        string[] GetAvailableDevices();

        /// <summary>Event fired when speech is being recorded.</summary>
        event EventHandler<bool>? IsRecordingChanged;
    }
}
