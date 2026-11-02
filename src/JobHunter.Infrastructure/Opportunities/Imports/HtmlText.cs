using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace JobHunter.Infrastructure.Opportunities.Imports;

/// <summary>
/// HTML reduced to readable plain text. Postings are untrusted, so this is all that's kept
/// of them: no markup survives, and they're only ever displayed as text (spec 02 §5).
/// </summary>
public static partial class HtmlText
{
    private static readonly HashSet<string> Dropped = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "template", "svg", "iframe", "object", "embed",
        "nav", "header", "footer", "aside", "form", "button", "select", "dialog",
    };

    private static readonly HashSet<string> Blocks = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "blockquote", "center", "dd", "details", "div", "dl", "dt", "figure", "h1", "h2", "h3",
        "h4", "h5", "h6", "hr", "li", "main", "ol", "p", "pre", "section", "summary", "table", "tr", "ul",
    };

    public static string From(string html)
    {
        var body = new HtmlParser().ParseDocument(html).Body;
        return body is null ? "" : From(body);
    }

    /// <summary>The text of one subtree (it is not modified).</summary>
    public static string From(INode root)
    {
        var builder = new StringBuilder();
        Walk(root, builder);

        var lines = builder.ToString().Split('\n').Select(line => Spaces().Replace(line, " ").Trim());
        var result = new StringBuilder();
        var pendingBlank = false;
        var bullet = false;
        var lastWasBullet = false;
        foreach (var line in lines)
        {
            // <li><p>Text</p></li> leaves its bullet alone on a line: join it to the text.
            if (line == "-")
            {
                bullet = true;
                continue;
            }
            if (line.Length == 0)
            {
                pendingBlank = result.Length > 0;
                continue;
            }

            var isBullet = bullet || line.StartsWith("- ", StringComparison.Ordinal);
            // Paragraphs keep a blank line between them; items of one list don't.
            if (pendingBlank && !(lastWasBullet && isBullet)) result.Append('\n');
            result.Append(bullet ? "- " : "").Append(line).Append('\n');

            pendingBlank = false;
            bullet = false;
            lastWasBullet = isBullet;
        }
        return result.ToString().Trim();
    }

    private static void Walk(INode node, StringBuilder text)
    {
        switch (node)
        {
            case IText t:
                text.Append(t.Data.Replace('\n', ' ').Replace('\r', ' '));
                return;
            case IElement e when Dropped.Contains(e.LocalName):
                return;
            case IElement { LocalName: "br" }:
                text.Append('\n');
                return;
            case IElement e when Blocks.Contains(e.LocalName):
                // List items are one line each; everything else is its own paragraph.
                text.Append(e.LocalName == "li" ? "\n- " : "\n\n");
                foreach (var child in e.ChildNodes) Walk(child, text);
                text.Append(e.LocalName == "li" ? "\n" : "\n\n");
                return;
        }

        foreach (var child in node.ChildNodes) Walk(child, text);
    }

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();
}
