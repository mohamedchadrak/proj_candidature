using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using PfeCopilot.Application.JobBoards;

namespace PfeCopilot.Infrastructure.JobBoards;

/// <summary>Télécharge une page d'offre d'emploi publique et en extrait le texte lisible (sans balisage).</summary>
public class OfferTextExtractor(IHttpClientFactory httpClientFactory) : IOfferTextExtractor
{
    private static readonly string[] IgnoredTags = ["script", "style", "noscript", "svg", "nav", "footer", "header"];

    public async Task<string> ExtractFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(OfferTextExtractor));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; PfeCopilotBot/1.0; +stage-pfe-tool)");

        var html = await client.GetStringAsync(url, cancellationToken);

        var context = BrowsingContext.New(Configuration.Default);
        using var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        var sb = new StringBuilder();
        AppendText(document.Body, sb);

        var text = sb.ToString();
        // Compacte les lignes vides multiples laissées par la mise en page HTML.
        while (text.Contains("\n\n\n"))
        {
            text = text.Replace("\n\n\n", "\n\n");
        }

        return text.Trim();
    }

    private static void AppendText(INode? node, StringBuilder sb)
    {
        if (node is null)
        {
            return;
        }

        if (node is IElement element && IgnoredTags.Contains(element.TagName.ToLowerInvariant()))
        {
            return;
        }

        if (node.NodeType == NodeType.Text)
        {
            var text = node.TextContent.Trim();
            if (text.Length > 0)
            {
                sb.Append(text).Append(' ');
            }
        }

        foreach (var child in node.ChildNodes)
        {
            AppendText(child, sb);
        }

        if (node is IElement el && (el.TagName.Equals("p", StringComparison.OrdinalIgnoreCase)
            || el.TagName.Equals("br", StringComparison.OrdinalIgnoreCase)
            || el.TagName.Equals("li", StringComparison.OrdinalIgnoreCase)
            || el.TagName.StartsWith('h')))
        {
            sb.Append('\n');
        }
    }
}
