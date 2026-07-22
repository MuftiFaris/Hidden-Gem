using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Assistant.ViewModels
{
    /// <summary>
    /// Base class that wires up INotifyPropertyChanged and provides
    /// the SetProperty helper for concise, boilerplate-free property setters.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Sets the backing field to <paramref name="value"/> and fires
        /// PropertyChanged only when the value actually changed.
        /// Returns true if the value changed.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}
