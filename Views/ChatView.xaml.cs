using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Assistant.Helpers;
using Assistant.Models;
using Assistant.Services;
using Assistant.ViewModels;
using Microsoft.Win32;

namespace Assistant.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _attached;
    private bool _scrollPending;
    public ChatView()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) => { Detach(); if (IsLoaded) Attach(); };
        AddHandler(RichTextBox.TextChangedEvent, new TextChangedEventHandler((_, args) =>
        {
            if (args.OriginalSource is Assistant.Controls.MarkdownView && MessagesScroller.VerticalOffset >= MessagesScroller.ScrollableHeight - 80) ScrollAfterLayout();
        }));
    }
    private void Attach() { if (DataContext is ChatViewModel vm && _attached != vm) { _attached = vm; vm.Messages.CollectionChanged += MessagesChanged; } }
    private void Detach() { if (_attached != null) _attached.Messages.CollectionChanged -= MessagesChanged; _attached = null; }
    private void MessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) { if (e.Action == NotifyCollectionChangedAction.Add) ScrollAfterLayout(); }
    private void ScrollAfterLayout()
    {
        if (_scrollPending) return;
        _scrollPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { _scrollPending = false; MessagesScroller.ScrollToEnd(); }));
    }
    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null)) { vm.SendCommand.Execute(null); e.Handled = true; }
    }
    private static ChatMessage? Message(object sender) => (sender as FrameworkElement)?.DataContext as ChatMessage;
    private void Copy(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { ActionStatus.Text = "Nothing to copy."; return; }
        try { Clipboard.SetText(value); ActionStatus.Text = "Copied to clipboard."; }
        catch (Exception) { ActionStatus.Text = "Clipboard is busy. Try again."; }
    }
    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (Message(sender) is { } message) Copy(message.Role == MessageRole.Assistant && !message.IsError ? MarkdownContent.PlainText(message.Content) : message.Content);
    }
    private List<ChatMessage> Conversation() => DataContext is ChatViewModel vm ? vm.Messages.Where(m => m.Role != MessageRole.System && !string.IsNullOrWhiteSpace(m.Content)).ToList() : new();
    private void CopyChat_Click(object sender, RoutedEventArgs e)
        => Copy(string.Join("\n\n", Conversation().Select(m => m.RoleDisplayName + "\n" + (m.Role == MessageRole.Assistant && !m.IsError ? MarkdownContent.PlainText(m.Content) : m.Content))));
    private void ExportMessage_Click(object sender, RoutedEventArgs e) { if (Message(sender) is { CanExport: true } message) ShowExportMenu((Button)sender, message); }
    private void ExportChat_Click(object sender, RoutedEventArgs e)
    {
        var messages = Conversation();
        if (messages.Count == 0) { ActionStatus.Text = "No messages to export."; return; }
        if (messages.Any(m => m.IsStreaming)) { ActionStatus.Text = "Wait for generation to finish before exporting chat."; return; }
        ShowExportMenu((Button)sender, null);
    }
    private void ShowExportMenu(Button owner, ChatMessage? message)
    {
        var menu = new ContextMenu { PlacementTarget = owner, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var format in new[] { ("Word document (.docx)", "docx"), ("Markdown (.md)", "md"), ("Plain text (.txt)", "txt") })
        {
            var item = new MenuItem { Header = format.Item1 }; item.Click += (_, _) => Export(message, format.Item2); menu.Items.Add(item);
        }
        if (message is { Role: MessageRole.Assistant }) { menu.Items.Add(new Separator()); var copy = new MenuItem { Header = "Copy Markdown" }; copy.Click += (_, _) => Copy(message.Content); menu.Items.Add(copy); }
        owner.ContextMenu = menu; menu.IsOpen = true;
    }
    private void Export(ChatMessage? message, string format)
    {
        var dialog = new SaveFileDialog { Title = "Export " + (message == null ? "conversation" : "message"), FileName = message == null ? "Conversation" : message.Role == MessageRole.User ? "Question" : "Response", DefaultExt = "." + format, AddExtension = true, OverwritePrompt = true, Filter = format == "docx" ? "Word document (*.docx)|*.docx" : format == "md" ? "Markdown (*.md)|*.md" : "Plain text (*.txt)|*.txt" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var messages = message == null ? Conversation() : new List<ChatMessage> { message };
            if (messages.Any(m => m.IsStreaming)) { ActionStatus.Text = "Generation is still running. Export after it finishes."; return; }
            if (format == "docx")
            {
                var exporter = new MessageExportService();
                if (message == null) exporter.ExportConversation(dialog.FileName, messages);
                else exporter.ExportDocx(dialog.FileName, message.Content, message.Role == MessageRole.User ? "Your question" : "Assistant response", message.Role == MessageRole.Assistant && !message.IsError);
            }
            else
            {
                var text = message == null
                    ? string.Join("\n\n", messages.Select(m => "## " + m.RoleDisplayName + "\n\n" + (format == "txt" && m.Role == MessageRole.Assistant ? MarkdownContent.PlainText(m.Content) : m.Content)))
                    : format == "txt" && message.Role == MessageRole.Assistant ? MarkdownContent.PlainText(message.Content) : message.Content;
                if (format == "txt" && message == null) text = string.Join("\n\n", messages.Select(m => m.RoleDisplayName + "\n" + (m.Role == MessageRole.Assistant ? MarkdownContent.PlainText(m.Content) : m.Content)));
                File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            }
            ActionStatus.Text = "Saved " + Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex) { ActionStatus.Text = "Export failed: " + ex.Message; }
    }
}
