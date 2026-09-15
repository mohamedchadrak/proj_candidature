using System.Text;
using PfeCopilot.Application.CvImport;
using UglyToad.PdfPig;

namespace PfeCopilot.Infrastructure.CvImport;

/// <summary>Extraction de texte PDF via PdfPig (bibliothèque open-source, pas de dépendance externe payante).</summary>
public class PdfTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        pdfStream.CopyTo(memory);
        memory.Position = 0;

        using var document = PdfDocument.Open(memory);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(page.Text);
        }

        return Task.FromResult(sb.ToString());
    }
}
