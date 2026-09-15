using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PfeCopilot.Application.Common;
using PfeCopilot.Domain.Entities;

namespace PfeCopilot.Infrastructure.Identity;

/// <summary>
/// DbContext unique de la solution : schéma ASP.NET Core Identity + entités métier multi-tenant.
/// Toutes les entités métier portent un UserId filtré automatiquement par un Global Query Filter
/// basé sur <see cref="ICurrentUserService"/>, pour empêcher toute fuite de données entre comptes.
/// Le worker de veille (sans utilisateur HTTP courant) doit utiliser IgnoreQueryFilters() et filtrer
/// lui-même explicitement par UserId lorsqu'il doit parcourir tous les comptes.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUser)
    : IdentityDbContext<ApplicationUser>(options), IAppDbContext
{
    private readonly ICurrentUserService _currentUser = currentUser;

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<CvFact> CvFacts => Set<CvFact>();
    public DbSet<ApiKeyConfig> ApiKeyConfigs => Set<ApiKeyConfig>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<JobApplicationVersion> JobApplicationVersions => Set<JobApplicationVersion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureUserOwned<CandidateProfile>(builder);
        ConfigureUserOwned<CvFact>(builder);
        ConfigureUserOwned<ApiKeyConfig>(builder);
        ConfigureUserOwned<JobOffer>(builder);
        ConfigureUserOwned<SavedSearch>(builder);
        ConfigureUserOwned<JobApplication>(builder);
        ConfigureUserOwned<JobApplicationVersion>(builder);

        builder.Entity<CvFact>().Property(f => f.Type).HasConversion<string>().HasMaxLength(64);
        builder.Entity<ApiKeyConfig>().Property(k => k.Provider).HasConversion<string>().HasMaxLength(32);
        builder.Entity<CandidateProfile>().Property(p => p.PreferredAiProvider).HasConversion<string>().HasMaxLength(32);
        builder.Entity<JobOffer>().Property(o => o.Source).HasConversion<string>().HasMaxLength(32);
        builder.Entity<JobApplication>().Property(a => a.Status).HasConversion<string>().HasMaxLength(32);

        builder.Entity<JobApplication>()
            .HasOne(a => a.JobOffer)
            .WithMany()
            .HasForeignKey(a => a.JobOfferId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<JobOffer>()
            .HasIndex(o => new { o.UserId, o.Source, o.ExternalId });
    }

    /// <summary>Ajoute l'index UserId et le filtre global multi-tenant à une entité <see cref="IUserOwned"/>.</summary>
    private void ConfigureUserOwned<TEntity>(ModelBuilder builder) where TEntity : class, IUserOwned
    {
        builder.Entity<TEntity>().HasIndex(nameof(IUserOwned.UserId));
        builder.Entity<TEntity>().HasQueryFilter(e => _currentUser.UserId == null || e.UserId == _currentUser.UserId);
    }
}
