using System.Text;

namespace PfeCopilot.Infrastructure.Latex;

/// <summary>Échappe les caractères spéciaux LaTeX dans du contenu dynamique (IA/utilisateur) avant injection dans un template.</summary>
public static class LatexEscaper
{
    public static string Escape(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(c switch
            {
                '&' => "\\&",
                '%' => "\\%",
                '$' => "\\$",
                '#' => "\\#",
                '_' => "\\_",
                '{' => "\\{",
                '}' => "\\}",
                '~' => "\\textasciitilde{}",
                '^' => "\\textasciicircum{}",
                '\\' => "\\textbackslash{}",
                _ => c.ToString()
            });
        }

        return sb.ToString();
    }
}
