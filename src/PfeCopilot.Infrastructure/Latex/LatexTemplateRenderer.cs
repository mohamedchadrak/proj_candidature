using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Application.Latex;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;
using Scriban;

namespace PfeCopilot.Infrastructure.Latex;

/// <summary>
/// Rend un CV/LM adapté en LaTeX. Volontairement en une seule colonne, sans tableaux ni images,
/// pour rester lisible par les ATS (voir PfeCopilot.Application.Applications.AtsScorer).
/// </summary>
public class LatexTemplateRenderer : ILatexTemplateRenderer
{
    private const string CvTemplate = """
        \documentclass[11pt,a4paper]{article}
        \usepackage[margin=2cm]{geometry}
        \usepackage[utf8]{inputenc}
        \usepackage[T1]{fontenc}
        \usepackage{titlesec}
        \usepackage{enumitem}
        \usepackage{hyperref}
        \pagestyle{empty}
        \setlist[itemize]{leftmargin=*,itemsep=1pt,topsep=2pt}
        \titleformat{\section}{\large\bfseries}{}{0em}{}[\titlerule]
        \titlespacing{\section}{0pt}{8pt}{4pt}

        \begin{document}

        \begin{center}
          {\Huge \textbf{ {{- full_name -}} }} \\[2pt]
          {{ targeted_specialty }} \\[2pt]
          {{ contact_line }}
        \end{center}

        \section*{Profil}
        {{ professional_summary }}

        {{ for section in sections }}
        \section*{ {{- section.title -}} }
        \begin{itemize}
        {{ for bullet in section.bullets }}
          \item {{ bullet }}
        {{ end }}
        \end{itemize}
        {{ end }}

        {{ if formation_items.size > 0 }}
        \section*{Formation}
        \begin{itemize}
        {{ for item in formation_items }}
          \item \textbf{ {{- item.title -}} } --- {{ item.organization }} ({{ item.period }})
        {{ end }}
        \end{itemize}
        {{ end }}

        {{ if languages_line != "" }}
        \section*{Langues \& Centres d'intérêt}
        {{ languages_line }}
        {{ end }}

        \end{document}
        """;

    private const string CoverLetterTemplate = """
        \documentclass[11pt,a4paper]{article}
        \usepackage[margin=2.5cm]{geometry}
        \usepackage[utf8]{inputenc}
        \usepackage[T1]{fontenc}
        \pagestyle{empty}

        \begin{document}

        \begin{flushright}
        {{ full_name }} \\
        {{ contact_line }}
        \end{flushright}

        \vspace{1cm}
        \textbf{Objet~: Candidature au poste de {{ job_title }}{{ if company_name != "" }} chez {{ company_name }}{{ end }}}

        \vspace{0.5cm}

        {{ body }}

        \vspace{1cm}
        {{ full_name }}

        \end{document}
        """;

    public string RenderCv(TailoredCvContent content, CandidateProfile profile, IReadOnlyCollection<CvFact> allFacts)
    {
        var contactParts = new[] { profile.Phone, profile.Email, profile.LinkedInUrl, profile.WebsiteUrl }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(LatexEscaper.Escape);

        var sections = content.Sections.Select(s => new
        {
            title = LatexEscaper.Escape(s.SectionTitle),
            bullets = s.Bullets.Select(b => LatexEscaper.Escape(b.Text)).ToList()
        }).ToList();

        var formationItems = allFacts
            .Where(f => f.Type == CvFactType.Formation)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new
            {
                title = LatexEscaper.Escape(f.Title),
                organization = LatexEscaper.Escape(f.Organization ?? string.Empty),
                period = FormatPeriod(f)
            }).ToList();

        var languageFacts = allFacts.Where(f => f.Type is CvFactType.Langue or CvFactType.CentreInteret)
            .Select(f => $"{f.Title}{(string.IsNullOrWhiteSpace(f.Description) ? "" : $" ({f.Description})")}");
        var languagesLine = LatexEscaper.Escape(string.Join(" — ", languageFacts));

        var template = Template.Parse(CvTemplate);
        return template.Render(new
        {
            full_name = LatexEscaper.Escape(profile.FullName),
            targeted_specialty = LatexEscaper.Escape(profile.TargetedSpecialty),
            contact_line = string.Join(" ~•~ ", contactParts),
            professional_summary = LatexEscaper.Escape(content.ProfessionalSummary),
            sections,
            formation_items = formationItems,
            languages_line = languagesLine
        });
    }

    public string RenderCoverLetter(string coverLetterText, CandidateProfile profile, string companyName, string jobTitle)
    {
        var contactParts = new[] { profile.Phone, profile.Email }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(LatexEscaper.Escape);

        var paragraphs = coverLetterText
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => LatexEscaper.Escape(p.Trim()));

        var template = Template.Parse(CoverLetterTemplate);
        return template.Render(new
        {
            full_name = LatexEscaper.Escape(profile.FullName),
            contact_line = string.Join(" ~•~ ", contactParts),
            job_title = LatexEscaper.Escape(jobTitle),
            company_name = LatexEscaper.Escape(companyName ?? string.Empty),
            body = string.Join("\n\n", paragraphs)
        });
    }

    private static string FormatPeriod(CvFact fact)
    {
        var start = fact.StartDate?.ToString("MM/yyyy") ?? "";
        var end = fact.EndDate?.ToString("MM/yyyy") ?? "présent";
        return $"{start} - {end}";
    }
}
