using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Assistant.Models;
using Assistant.ViewModels;

namespace Assistant.Views
{
    public partial class ChatView : UserControl
    {
        public ChatView()
        {
            InitializeComponent();

            // Auto-scroll to bottom whenever a new message is added or an
            // existing item's Content changes (via INotifyPropertyChanged on ChatMessage,
            // which triggers ItemsControl re-layout, observed by LayoutUpdated).
            Loaded += (_, _) =>
            {
                if (DataContext is ChatViewModel vm)
                {
                    // Subscribe to collection changes to scroll to bottom
                    vm.Messages.CollectionChanged += Messages_CollectionChanged;
                }
            };

            Unloaded += (_, _) =>
            {
                if (DataContext is ChatViewModel vm)
                    vm.Messages.CollectionChanged -= Messages_CollectionChanged;
            };

            // LayoutUpdated fires after rendering; we use it to track streaming updates
            MessagesScroller.LayoutUpdated += (_, _) =>
            {
                // Only auto-scroll if already near the bottom (within 80 px)
                var sv = MessagesScroller;
                bool nearBottom = sv.VerticalOffset >= sv.ScrollableHeight - 80;
                if (nearBottom)
                    sv.ScrollToEnd();
            };
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Scroll to bottom when a new message bubble is added
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.BeginInvoke(() => MessagesScroller.ScrollToEnd());
        }

        /// <summary>
        /// Send on Enter; Shift+Enter inserts a newline (multi-line input).
        /// </summary>
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
