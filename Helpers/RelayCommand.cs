using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assistant.Helpers
{
    /// <summary>
    /// Synchronous ICommand implementation.
    /// CanExecuteChanged is wired to CommandManager.RequerySuggested so WPF
    /// automatically re-evaluates button states after focus changes and other UI events.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?>   _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>Convenience constructor for parameterless actions.</summary>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

        public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
        public void Execute(object? p)    => _execute(p);

        /// <summary>Manually triggers a CanExecute re-evaluation.</summary>
        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Async ICommand implementation that prevents re-entrant execution while a
    /// task is running.  Exceptions are NOT swallowed — callers should try/catch
    /// inside the delegate.
    /// </summary>
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Func<object?, bool>? _canExecute;
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }

        public bool CanExecute(object? p) => !_isRunning && (_canExecute?.Invoke(p) ?? true);

        public async void Execute(object? p)
        {
            if (!CanExecute(p)) return;
            _isRunning = true;
            CommandManager.InvalidateRequerySuggested();
            try   { await _execute(p).ConfigureAwait(true); }
            finally
            {
                _isRunning = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
