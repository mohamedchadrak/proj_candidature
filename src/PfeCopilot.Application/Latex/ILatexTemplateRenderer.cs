using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Application.Latex;

/// <summary>Injecte un CV adapté + une lettre de motivation dans un template LaTeX.</summary>
public interface ILatexTemplateRenderer
{
    string RenderCv(TailoredCvContent content, CandidateProfile profile, IReadOnlyCollection<CvFact> allFacts);
    string RenderCoverLetter(string coverLetterText, CandidateProfile profile, string companyName, string jobTitle);
}
