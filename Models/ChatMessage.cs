using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Assistant.Models
{
    public enum MessageRole
    {
        User,
        Assistant,
        System
    }

    /// <summary>
    /// Represents a single message in the conversation.
    /// Implements INotifyPropertyChanged so that streaming content updates
    /// propagate directly to WPF bindings without replacing items in the collection.
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content = string.Empty;
        private bool _isStreaming;
        private bool _isError;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Guid Id { get; init; } = Guid.NewGuid();
        public MessageRole Role { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        /// <summary>True while the assistant is still streaming a response.</summary>
        public bool IsStreaming
        {
            get => _isStreaming;
            set { _isStreaming = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
        }

        /// <summary>True when the message represents an API or network error.</summary>
        public bool IsError
        {
            get => _isError;
            set { _isError = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusIcon)); }
        }

        public string RoleDisplayName => Role switch
        {
            MessageRole.User      => "You",
            MessageRole.Assistant => "Gemini",
            MessageRole.System    => "System",
            _                     => "Unknown"
        };

        public string StatusIcon => IsStreaming ? "⏳" : IsError ? "❌" : string.Empty;
    }
}
