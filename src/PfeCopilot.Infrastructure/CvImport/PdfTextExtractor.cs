using System.Text;
using PfeCopilot.Application.CvImport;
using UglyToad.PdfPig;

namespace PfeCopilot.Infrastructure.CvImport;

/// <summary>Extraction de texte PDF via PdfPig (bibliothèque open-source, pas de dépendance externe payante).</summary>
public class PdfTextExtractor : IPdfTextExtractor
{
    public async Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        // Le stream fourni par Blazor Server (IBrowserFile.OpenReadStream) n'autorise pas les
        // lectures synchrones : Stream.CopyTo lève "Synchronous reads are not supported.".
        await pdfStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var document = PdfDocument.Open(memory);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }
}
