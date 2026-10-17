using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Assistant.Helpers;
using Assistant.Models;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdTable = Markdig.Extensions.Tables.Table;
using MdRow = Markdig.Extensions.Tables.TableRow;
using MdCell = Markdig.Extensions.Tables.TableCell;

namespace Assistant.Services;

/// <summary>Creates real DOCX packages locally from the same Markdown AST used by chat.</summary>
public sealed class MessageExportService
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private readonly List<XElement> _links = new();
    private readonly List<XElement> _numbering = new();
    private static XElement E(string name, params object[] content) => new(W + name, content);
    private static XAttribute V(object value) => new(W + "val", value);
    private static string Clean(string text)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is 9 or 10 or 13 || rune.Value >= 32 && rune.Value != 0xfffe && rune.Value != 0xffff) result.Append(rune.ToString());
        return result.ToString();
    }
    public void ExportDocx(string path, string text, string? title = null, bool parseMarkdown = true)
    {
        _links.Clear(); _numbering.Clear();
        var body = E("body");
        if (!string.IsNullOrWhiteSpace(title)) body.Add(P(title, "Title"));
        if (parseMarkdown) AddBlocks(body, MarkdownContent.Parse(text));
        else foreach (var line in text.Split('\n')) body.Add(P(line.TrimEnd('\r')));
        Save(path, body);
    }
    public void ExportConversation(string path, IEnumerable<ChatMessage> messages)
    {
        _links.Clear(); _numbering.Clear();
        var body = E("body", P("Conversation", "Title"));
        foreach (var message in messages.Where(m => m.Role != MessageRole.System && !string.IsNullOrWhiteSpace(m.Content)))
        {
            body.Add(P(message.RoleDisplayName, "Heading1"));
            if (message.Role == MessageRole.Assistant && !message.IsError) AddBlocks(body, MarkdownContent.Parse(message.Content));
            else body.Add(P(message.Content));
        }
        Save(path, body);
    }
    private void AddBlocks(XElement output, ContainerBlock blocks, int depth = 0)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case HeadingBlock heading: output.Add(Paragraph(heading, "Heading" + Math.Min(heading.Level, 6))); break;
                case CodeBlock code:
                    foreach (var line in code.Lines.ToString().Split('\n')) output.Add(P(line.TrimEnd('\r'), "Code"));
                    break;
                case ListBlock list:
                    int numberId = _numbering.Count + 1;
                    int start = int.TryParse(list.OrderedStart, out var value) ? value : 1;
                    _numbering.Add(E("abstractNum", new XAttribute(W + "abstractNumId", numberId), E("multiLevelType", V("singleLevel")), E("lvl", new XAttribute(W + "ilvl", 0), E("start", V(start)), E("numFmt", V(list.IsOrdered ? "decimal" : "bullet")), E("lvlText", V(list.IsOrdered ? "%1." : "•")), E("lvlJc", V("left")), E("pPr", E("tabs", E("tab", V("num"), new XAttribute(W + "pos", 360))), E("ind", new XAttribute(W + "left", 360), new XAttribute(W + "hanging", 240))))));
                    foreach (ListItemBlock item in list)
                    {
                        var holder = E("holder"); AddBlocks(holder, item, depth + 1);
                        var first = holder.Elements(W + "p").FirstOrDefault();
                        if (first == null) { first = P(""); holder.AddFirst(first); }
                        var properties = first.Element(W + "pPr"); if (properties == null) { properties = E("pPr"); first.AddFirst(properties); }
                        properties.Add(E("numPr", E("ilvl", V(0)), E("numId", V(numberId))), E("ind", new XAttribute(W + "left", 360 * (depth + 1)), new XAttribute(W + "hanging", 240)));
                        output.Add(holder.Elements().ToList());
                    }
                    break;
                case MdTable table:
                    var rows = table.Cast<MdRow>().ToList();
                    int columns = Math.Max(1, rows.Select(r => r.Count).DefaultIfEmpty(1).Max());
                    int cellWidth = 9360 / columns;
                    var wordTable = E("tbl", E("tblPr", E("tblW", new XAttribute(W + "w", 9360), new XAttribute(W + "type", "dxa")), E("tblLayout", new XAttribute(W + "type", "fixed")), E("tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side => E(side, new XAttribute(W + "w", side is "top" or "bottom" ? 90 : 120), new XAttribute(W + "type", "dxa")))), E("tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side => E(side, V("single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "D9DDE3"))))), E("tblGrid", Enumerable.Range(0, columns).Select(_ => E("gridCol", new XAttribute(W + "w", cellWidth)))));
                    foreach (var row in rows)
                    {
                        var tr = E("tr"); if (row.IsHeader) tr.Add(E("trPr", E("tblHeader")));
                        foreach (MdCell cell in row)
                        {
                            var tc = E("tc", E("tcPr", E("tcW", new XAttribute(W + "w", cellWidth), new XAttribute(W + "type", "dxa"))));
                            AddBlocks(tc, cell, depth);
                            if (!tc.Elements(W + "p").Any()) tc.Add(P(""));
                            if (row.IsHeader)
                            {
                                tc.Element(W + "tcPr")!.Add(E("shd", new XAttribute(W + "fill", "F2F4F7")));
                                foreach (var run in tc.Descendants(W + "r")) { var props = run.Element(W + "rPr"); if (props == null) { props = E("rPr"); run.AddFirst(props); } props.Add(E("b")); }
                            }
                            tr.Add(tc);
                        }
                        wordTable.Add(tr);
                    }
                    output.Add(wordTable, P("")); break;
                case QuoteBlock quote: AddBlocks(output, quote, depth); break;
                case ThematicBreakBlock: output.Add(P("")); break;
                case LeafBlock leaf: output.Add(Paragraph(leaf)); break;
                case ContainerBlock nested: AddBlocks(output, nested, depth); break;
            }
        }
    }
    private XElement Paragraph(LeafBlock block, string? style = null)
    {
        var p = E("p"); if (style != null) p.Add(E("pPr", E("pStyle", V(style))));
        AddInlines(p, block.Inline, false, false, false);
        return p;
    }
    private void AddInlines(XElement output, ContainerInline? container, bool bold, bool italic, bool strike)
    {
        if (container == null) return;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: output.Add(Run(literal.Content.ToString(), bold, italic, strike)); break;
                case CodeInline code: output.Add(Run(code.Content, bold, italic, strike, true)); break;
                case LineBreakInline line: output.Add(line.IsHard ? E("r", E("br")) : Run(" ", bold, italic, strike)); break;
                case EmphasisInline emphasis:
                    AddInlines(output, emphasis, bold || emphasis.DelimiterChar != '~' && emphasis.DelimiterCount >= 2, italic || emphasis.DelimiterChar != '~' && emphasis.DelimiterCount % 2 == 1, strike || emphasis.DelimiterChar == '~'); break;
                case LinkInline link when link.IsImage: output.Add(Run("[Image: " + MarkdownContent.InlineText(link) + "]", bold, italic, strike)); break;
                case LinkInline link:
                    if (MarkdownContent.SafeLink(link.Url, out var uri))
                    {
                        var id = "link" + (_links.Count + 1);
                        _links.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/hyperlink"), new XAttribute("Target", uri.AbsoluteUri), new XAttribute("TargetMode", "External")));
                        var hyperlink = E("hyperlink", new XAttribute(R + "id", id)); AddInlines(hyperlink, link, bold, italic, strike); output.Add(hyperlink);
                    }
                    else AddInlines(output, link, bold, italic, strike);
                    break;
                case AutolinkInline link: output.Add(Run(link.Url, bold, italic, strike)); break;
                case ContainerInline nested: AddInlines(output, nested, bold, italic, strike); break;
            }
        }
    }
    private static XElement Run(string text, bool bold = false, bool italic = false, bool strike = false, bool code = false)
    {
        var run = E("r"); var properties = E("rPr");
        if (bold) properties.Add(E("b")); if (italic) properties.Add(E("i")); if (strike) properties.Add(E("strike"));
        if (code) properties.Add(E("rFonts", new XAttribute(W + "ascii", "Consolas"), new XAttribute(W + "hAnsi", "Consolas")), E("sz", V(20)));
        if (properties.HasElements) run.Add(properties);
        var lines = Clean(text).Split('\n');
        for (int i = 0; i < lines.Length; i++) { if (i > 0) run.Add(E("br")); run.Add(E("t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[i])); }
        return run;
    }
    private static XElement P(string text, string? style = null) => E("p", style == null ? Array.Empty<XElement>() : new[] { E("pPr", E("pStyle", V(style))) }, Run(text));
    private static XElement Styles()
    {
        var styles = E("styles", E("docDefaults", E("rPrDefault", E("rPr", E("rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri")), E("sz", V(22)), E("color", V("111111")))), E("pPrDefault", E("pPr", E("spacing", new XAttribute(W + "after", 140), new XAttribute(W + "line", 280), new XAttribute(W + "lineRule", "auto"))))));
        styles.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "default", 1), new XAttribute(W + "styleId", "Normal"), E("name", V("Normal"))));
        styles.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "Title"), E("name", V("Title")), E("basedOn", V("Normal")), E("pPr", E("keepNext"), E("spacing", new XAttribute(W + "after", 260))), E("rPr", E("b"), E("sz", V(40)))));
        for (int level = 1; level <= 6; level++) styles.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "Heading" + level), E("name", V("heading " + level)), E("basedOn", V("Normal")), E("pPr", E("keepNext"), E("outlineLvl", V(level - 1)), E("spacing", new XAttribute(W + "before", 240), new XAttribute(W + "after", 100))), E("rPr", E("b"), E("sz", V(level == 1 ? 30 : level == 2 ? 26 : 23)))));
        styles.Add(E("style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "styleId", "Code"), E("name", V("Code")), E("basedOn", V("Normal")), E("pPr", E("spacing", new XAttribute(W + "after", 0), new XAttribute(W + "line", 240))), E("rPr", E("rFonts", new XAttribute(W + "ascii", "Consolas"), new XAttribute(W + "hAnsi", "Consolas")), E("sz", V(19)))));
        return styles;
    }
    private void Save(string path, XElement body)
    {
        body.Add(E("sectPr", E("pgSz", new XAttribute(W + "w", 12240), new XAttribute(W + "h", 15840)), E("pgMar", new XAttribute(W + "top", 1440), new XAttribute(W + "bottom", 1440), new XAttribute(W + "left", 1440), new XAttribute(W + "right", 1440))));
        var relationships = new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", R.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")), new XElement(Rel + "Relationship", new XAttribute("Id", "numbering"), new XAttribute("Type", R.NamespaceName + "/numbering"), new XAttribute("Target", "numbering.xml")), _links);
        var numbers = E("numbering", _numbering, _numbering.Select(n => E("num", new XAttribute(W + "numId", n.Attribute(W + "abstractNumId")!.Value), E("abstractNumId", V(n.Attribute(W + "abstractNumId")!.Value)))));
        XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = new XElement(content + "Types", new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new[] { ("document", "document.main"), ("styles", "styles"), ("numbering", "numbering") }.Select(part => new XElement(content + "Override", new XAttribute("PartName", "/word/" + part.Item1 + ".xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml." + part.Item2 + "+xml"))));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                Write(archive, "[Content_Types].xml", types);
                Write(archive, "_rels/.rels", new XElement(Rel + "Relationships", new XElement(Rel + "Relationship", new XAttribute("Id", "document"), new XAttribute("Type", R.NamespaceName + "/officeDocument"), new XAttribute("Target", "word/document.xml"))));
                Write(archive, "word/document.xml", new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), body));
                Write(archive, "word/styles.xml", Styles()); Write(archive, "word/numbering.xml", numbers); Write(archive, "word/_rels/document.xml.rels", relationships);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Write(ZipArchive archive, string name, XElement xml)
    {
        foreach (var properties in xml.DescendantsAndSelf().Where(e => e.Name.Namespace == W && e.Name.LocalName is "pPr" or "rPr" or "tblPr"))
        {
            var order = properties.Name.LocalName switch
            {
                "pPr" => new[] { "pStyle", "keepNext", "keepLines", "numPr", "tabs", "spacing", "ind", "outlineLvl" },
                "rPr" => new[] { "rFonts", "b", "i", "strike", "color", "sz" },
                _ => new[] { "tblW", "tblBorders", "tblLayout", "tblCellMar" }
            };
            properties.ReplaceNodes(properties.Elements().OrderBy(e => { var index = Array.IndexOf(order, e.Name.LocalName); return index < 0 ? 99 : index; }).ToList());
        }
        using var stream = archive.CreateEntry(name).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), xml).Save(stream);
    }
}
