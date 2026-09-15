using PfeCopilot.Application.Ai;
using PfeCopilot.Application.Ai.Models;
using PfeCopilot.Domain.Entities;
using PfeCopilot.Domain.Enums;
using Xunit;

namespace PfeCopilot.Tests;

public class AiProviderSelectorTests
{
    private class FakeProvider(AiProviderType type) : IAiProvider
    {
        public AiProviderType ProviderType { get; } = type;

        public Task<JobRequirements> ExtractRequirementsAsync(string offerText, string apiKeyPlainText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ExtractedCvContent> ExtractCvFactsAsync(string cvText, string apiKeyPlainText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> ValidateApiKeyAsync(string apiKeyPlainText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TailoredCvContent> GenerateTailoredCvAsync(IReadOnlyCollection<CvFact> facts, JobRequirements requirements, CandidateProfile profile, string apiKeyPlainText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string> GenerateCoverLetterAsync(IReadOnlyCollection<CvFact> facts, JobRequirements requirements, CandidateProfile profile, string offerText, string apiKeyPlainText, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private static ApiKeyConfig MakeKey(AiProviderType provider, DateTime createdAt) => new()
    {
        UserId = "user-1",
        Provider = provider,
        CipherText = "cipher",
        IsActive = true,
        CreatedAtUtc = createdAt
    };

    [Fact]
    public void SelectActiveProvider_PicksPreferredProvider_WhenActiveKeyExists()
    {
        var providers = new IAiProvider[] { new FakeProvider(AiProviderType.Claude), new FakeProvider(AiProviderType.Gemini) };
        var keys = new[] { MakeKey(AiProviderType.Claude, DateTime.UtcNow), MakeKey(AiProviderType.Gemini, DateTime.UtcNow) };

        var (provider, key) = AiProviderSelector.SelectActiveProvider(providers, keys, AiProviderType.Gemini);

        Assert.Equal(AiProviderType.Gemini, provider.ProviderType);
        Assert.Equal(AiProviderType.Gemini, key.Provider);
    }

    [Fact]
    public void SelectActiveProvider_FallsBackToAnyActiveKey_WhenPreferredHasNone()
    {
        var providers = new IAiProvider[] { new FakeProvider(AiProviderType.Claude), new FakeProvider(AiProviderType.Gemini) };
        var keys = new[] { MakeKey(AiProviderType.Claude, DateTime.UtcNow) };

        var (provider, _) = AiProviderSelector.SelectActiveProvider(providers, keys, AiProviderType.Gemini);

        Assert.Equal(AiProviderType.Claude, provider.ProviderType);
    }

    [Fact]
    public void SelectActiveProvider_UsesMostRecentKey_WhenMultipleActiveForSameProvider()
    {
        var providers = new IAiProvider[] { new FakeProvider(AiProviderType.Claude) };
        var older = MakeKey(AiProviderType.Claude, DateTime.UtcNow.AddDays(-2));
        var newer = MakeKey(AiProviderType.Claude, DateTime.UtcNow);

        var (_, key) = AiProviderSelector.SelectActiveProvider(providers, [older, newer], AiProviderType.Claude);

        Assert.Equal(newer.Id, key.Id);
    }

    [Fact]
    public void SelectActiveProvider_Throws_WhenNoActiveKeys()
    {
        var providers = new IAiProvider[] { new FakeProvider(AiProviderType.Claude) };

        Assert.Throws<InvalidOperationException>(() =>
            AiProviderSelector.SelectActiveProvider(providers, [], AiProviderType.Claude));
    }

    [Fact]
    public void SelectActiveProvider_Throws_WhenActiveKeyHasNoMatchingProviderImplementation()
    {
        var providers = new IAiProvider[] { new FakeProvider(AiProviderType.Claude) };
        var keys = new[] { MakeKey(AiProviderType.OpenAi, DateTime.UtcNow) };

        Assert.Throws<InvalidOperationException>(() =>
            AiProviderSelector.SelectActiveProvider(providers, keys, AiProviderType.OpenAi));
    }
}
