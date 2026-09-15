namespace PfeCopilot.Application.Latex;

public record LatexCompilationResult(bool Success, byte[]? PdfBytes, string Log);

/// <summary>Compile un document LaTeX en PDF via une distribution TeX installée localement (MiKTeX/TeX Live).</summary>
public interface ILatexPdfCompiler
{
    Task<LatexCompilationResult> CompileAsync(string latexSource, CancellationToken cancellationToken = default);
}
