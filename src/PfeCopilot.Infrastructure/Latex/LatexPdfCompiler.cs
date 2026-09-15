using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using PfeCopilot.Application.Latex;

namespace PfeCopilot.Infrastructure.Latex;

/// <summary>
/// Compile un document LaTeX en PDF en appelant pdflatex en subprocess. Nécessite une
/// distribution TeX installée localement (MiKTeX ou TeX Live) avec pdflatex dans le PATH.
/// </summary>
public class LatexPdfCompiler(ILogger<LatexPdfCompiler> logger) : ILatexPdfCompiler
{
    public async Task<LatexCompilationResult> CompileAsync(string latexSource, CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "pfe-copilot-latex", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var texPath = Path.Combine(workDir, "document.tex");
        var pdfPath = Path.Combine(workDir, "document.pdf");

        try
        {
            await File.WriteAllTextAsync(texPath, latexSource, Encoding.UTF8, cancellationToken);

            var logBuilder = new StringBuilder();

            // pdflatex doit tourner deux fois pour résoudre les références (sommaire, hyperliens) — on reste simple ici.
            for (var pass = 0; pass < 2; pass++)
            {
                var passLog = await RunPdfLatexAsync(workDir, texPath, cancellationToken);
                logBuilder.AppendLine($"--- Passe {pass + 1} ---");
                logBuilder.AppendLine(passLog);
            }

            if (!File.Exists(pdfPath))
            {
                logger.LogWarning("Compilation LaTeX échouée, aucun PDF produit. Log : {Log}", logBuilder);
                return new LatexCompilationResult(false, null, logBuilder.ToString());
            }

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
            return new LatexCompilationResult(true, pdfBytes, logBuilder.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Erreur lors de la compilation LaTeX.");
            return new LatexCompilationResult(false, null, ex.Message);
        }
        finally
        {
            TryCleanup(workDir);
        }
    }

    private static async Task<string> RunPdfLatexAsync(string workDir, string texPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pdflatex",
            ArgumentList = { "-interaction=nonstopmode", "-halt-on-error", "-output-directory", workDir, texPath },
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "pdflatex introuvable dans le PATH. Installez une distribution TeX locale (MiKTeX ou TeX Live) pour compiler les CV/LM en PDF.", ex);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return await stdOutTask + Environment.NewLine + await stdErrTask;
    }

    private static void TryCleanup(string workDir)
    {
        try
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
        catch
        {
            // best-effort : le nettoyage du répertoire temporaire n'est pas critique.
        }
    }
}
