using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Assistant.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdRow = Markdig.Extensions.Tables.TableRow;
using MdCell = Markdig.Extensions.Tables.TableCell;
using WpfBlock = System.Windows.Documents.Block;

namespace Assistant.Controls;

/// <summary>Selectable native Markdown; no HTML execution or automatic remote downloads.</summary>
public sealed class MarkdownView : RichTextBox
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarkdownView), new PropertyMetadata("", Changed));
    public static readonly DependencyProperty ParseMarkdownProperty = DependencyProperty.Register(nameof(ParseMarkdown), typeof(bool), typeof(MarkdownView), new PropertyMetadata(true, Changed));
    public static readonly DependencyProperty IsStreamingProperty = DependencyProperty.Register(nameof(IsStreaming), typeof(bool), typeof(MarkdownView), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(nameof(IsError), typeof(bool), typeof(MarkdownView), new PropertyMetadata(false, Changed));
    public bool IsError { get => (bool)GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }
    private readonly DispatcherTimer _renderTimer;
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool ParseMarkdown { get => (bool)GetValue(ParseMarkdownProperty); set => SetValue(ParseMarkdownProperty, value); }
    public bool IsStreaming { get => (bool)GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }
    public MarkdownView()
    {
        IsReadOnly = true; IsDocumentEnabled = true;
        BorderThickness = new Thickness(0); Background = Brushes.Transparent; Padding = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled; HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => RenderText(), Dispatcher);
        _renderTimer.Stop();
        Unloaded += (_, _) => _renderTimer.Stop();
        Loaded += (_, _) => RenderText();
    }
    private Brush Brush(string name) => TryFindResource(name) as Brush ?? Brushes.Gray;
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (MarkdownView)target;
        if (view._renderTimer == null) return;
        if (view.IsStreaming && view.IsLoaded) { if (!view._renderTimer.IsEnabled) view._renderTimer.Start(); }
        else view.RenderText();
    }
    private void RenderText()
    {
        _renderTimer.Stop();
        var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 14, LineHeight = 22, PagePadding = new Thickness(0), Foreground = Brush(IsError ? "ErrorBrush" : "PrimaryTextBrush") };
        if (ParseMarkdown) AddBlocks(document.Blocks, MarkdownContent.Parse(Text));
        else document.Blocks.Add(new Paragraph(new Run(Text ?? "")) { Margin = new Thickness(0) });
        Document = document;
    }
    private void AddBlocks(BlockCollection output, ContainerBlock blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    var title = Paragraph(heading); title.FontSize = heading.Level switch { 1 => 24, 2 => 21, 3 => 18, _ => 16 };
                    title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0, 16, 0, 8); output.Add(title); break;
                case CodeBlock code: output.Add(Code(code)); break;
                case ListBlock list:
                    var renderedList = new System.Windows.Documents.List { MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(24, 0, 0, 0) };
                    if (list.IsOrdered && int.TryParse(list.OrderedStart, out var number) && number > 0) renderedList.StartIndex = number;
                    foreach (ListItemBlock item in list) { var renderedItem = new ListItem(); AddBlocks(renderedItem.Blocks, item); if (renderedItem.Blocks.Count == 0) renderedItem.Blocks.Add(new Paragraph()); renderedList.ListItems.Add(renderedItem); }
                    output.Add(renderedList); break;
                case QuoteBlock quote:
                    var section = new Section { BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(14, 0, 0, 0), Margin = new Thickness(0, 8, 0, 12) };
                    AddBlocks(section.Blocks, quote); output.Add(section); break;
                case MdTable table:
                    var renderedTable = new System.Windows.Documents.Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 16) };
                    var group = new TableRowGroup(); renderedTable.RowGroups.Add(group);
                    foreach (MdRow row in table)
                    {
                        var renderedRow = new System.Windows.Documents.TableRow(); group.Rows.Add(renderedRow);
                        foreach (MdCell cell in row)
                        {
                            var renderedCell = new System.Windows.Documents.TableCell { BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(0.5), Padding = new Thickness(9, 7, 9, 7) };
                            if (row.IsHeader) { renderedCell.FontWeight = FontWeights.SemiBold; renderedCell.Background = Brush("Surface2Brush"); }
                            AddBlocks(renderedCell.Blocks, cell); if (renderedCell.Blocks.Count == 0) renderedCell.Blocks.Add(new Paragraph());
                            renderedRow.Cells.Add(renderedCell);
                        }
                    }
                    output.Add(renderedTable); break;
                case ThematicBreakBlock: output.Add(new Paragraph { BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 8, 0, 16), FontSize = 1 }); break;
                case LeafBlock leaf: output.Add(Paragraph(leaf)); break;
                case ContainerBlock nested: AddBlocks(output, nested); break;
            }
        }
    }
    private Paragraph Paragraph(LeafBlock leaf)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };
        AddInlines(paragraph.Inlines, leaf.Inline);
        return paragraph;
    }
    private void AddInlines(InlineCollection output, ContainerInline? container)
    {
        if (container == null) return;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: output.Add(new Run(literal.Content.ToString())); break;
                case CodeInline code: output.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), FontSize = 13, Background = Brush("Surface2Brush") }); break;
                case LineBreakInline line: output.Add(line.IsHard ? new LineBreak() : new Run(" ")); break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else { if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold; if (emphasis.DelimiterCount % 2 == 1) span.FontStyle = FontStyles.Italic; }
                    AddInlines(span.Inlines, emphasis); output.Add(span); break;
                case LinkInline link when link.IsImage: output.Add(new Run("[Image: " + MarkdownContent.InlineText(link) + "]")); break;
                case LinkInline link:
                    if (MarkdownContent.SafeLink(link.Url, out var uri))
                    {
                        var hyperlink = new Hyperlink { NavigateUri = uri, Foreground = Brush("AccentBrush") };
                        AddInlines(hyperlink.Inlines, link); hyperlink.RequestNavigate += (_, e) => { try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { } e.Handled = true; }; output.Add(hyperlink);
                    }
                    else AddInlines(output, link);
                    break;
                case AutolinkInline link: output.Add(new Run(link.Url) { Foreground = Brush("AccentBrush") }); break;
                case ContainerInline nested: AddInlines(output, nested); break;
            }
        }
    }
    private WpfBlock Code(CodeBlock code)
    {
        var language = code is FencedCodeBlock fence ? fence.Info ?? "code" : "code";
        var raw = code.Lines.ToString();
        var panel = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
        var copy = new Button { Content = "Copy code", Style = TryFindResource("SecondaryButton") as Style, Padding = new Thickness(8, 3, 8, 3), FontSize = 11 };
        DockPanel.SetDock(copy, Dock.Right); copy.Click += (_, _) => { try { Clipboard.SetText(raw); copy.Content = "Copied"; } catch { copy.Content = "Copy failed"; } };
        header.Children.Add(copy); header.Children.Add(new TextBlock { Text = language, Foreground = Brush("SecondaryTextBrush"), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
        panel.Children.Add(header);
        panel.Children.Add(new TextBox { Text = raw, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, Background = Brushes.Transparent, Foreground = Brush("PrimaryTextBrush"), BorderThickness = new Thickness(0), Padding = new Thickness(0), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        return new BlockUIContainer(new Border { Child = panel, Background = Brush("SurfaceBrush"), BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(12) }) { Margin = new Thickness(0, 6, 0, 14) };
    }
}
