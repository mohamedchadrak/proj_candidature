using Microsoft.EntityFrameworkCore;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Application.Common;

/// <summary>
/// Vue "métier" de la base exposée à la couche Application, indépendante du fournisseur EF Core
/// (implémentée par PfeCopilot.Infrastructure.Identity.ApplicationDbContext).
/// </summary>
public interface IAppDbContext
{
    DbSet<CandidateProfile> CandidateProfiles { get; }
    DbSet<CvFact> CvFacts { get; }
    DbSet<ApiKeyConfig> ApiKeyConfigs { get; }
    DbSet<JobOffer> JobOffers { get; }
    DbSet<SavedSearch> SavedSearches { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<JobApplicationVersion> JobApplicationVersions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
