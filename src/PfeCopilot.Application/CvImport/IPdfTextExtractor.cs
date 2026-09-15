namespace PfeCopilot.Application.CvImport;

/// <summary>Extrait le texte brut d'un fichier PDF (le CV source uploadé par l'utilisateur).</summary>
public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
