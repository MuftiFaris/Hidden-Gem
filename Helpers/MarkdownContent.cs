using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;

namespace Assistant.Helpers;

public static class MarkdownContent
{
    public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseAutoLinks().UseEmphasisExtras().DisableHtml().Build();
    public static MarkdownDocument Parse(string text) => Markdown.Parse(text ?? "", Pipeline);
    public static string InlineText(ContainerInline? container)
    {
        if (container == null) return "";
        var text = new StringBuilder();
        foreach (var item in container)
        {
            switch (item)
            {
                case LiteralInline literal: text.Append(literal.Content.ToString()); break;
                case CodeInline code: text.Append(code.Content); break;
                case LineBreakInline line: text.Append(line.IsHard ? '\n' : ' '); break;
                case AutolinkInline link: text.Append(link.Url); break;
                case ContainerInline nested: text.Append(InlineText(nested)); break;
            }
        }
        return text.ToString();
    }
    public static string PlainText(string markdown)
    {
        var text = new StringBuilder();
        Write(Parse(markdown), text, 0);
        return text.ToString().TrimEnd();
    }
    private static void Write(ContainerBlock blocks, StringBuilder text, int depth)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case CodeBlock code: text.AppendLine(code.Lines.ToString()).AppendLine(); break;
                case ListBlock list:
                    int number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                    foreach (ListItemBlock item in list)
                    {
                        text.Append(new string(' ', depth * 2)).Append(list.IsOrdered ? $"{number++}. " : "• ");
                        var inner = new StringBuilder(); Write(item, inner, depth + 1);
                        text.AppendLine(inner.ToString().TrimEnd());
                    }
                    text.AppendLine(); break;
                case Markdig.Extensions.Tables.Table table:
                    foreach (TableRow row in table)
                        text.AppendLine(string.Join(" | ", row.Cast<TableCell>().Select(cell => { var value = new StringBuilder(); Write(cell, value, depth); return value.ToString().Trim(); })));
                    text.AppendLine(); break;
                case LeafBlock leaf: text.AppendLine(InlineText(leaf.Inline)).AppendLine(); break;
                case ContainerBlock nested: Write(nested, text, depth); break;
            }
        }
    }
    public static bool SafeLink(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var result) && result.Scheme is "http" or "https") { uri = result; return true; }
        uri = null!; return false;
    }
}
