using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;
using FluentAssertions;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class RequirementCacheTests
    {
        private string CreateCacheDir() =>
            Path.Combine(Directory.GetCurrentDirectory(),
                $"TestReqCache_{Guid.NewGuid():N}");

        private List<ExtractedRequirement> CreateFakeRequirements(int count) =>
                    Enumerable.Range(1, count).Select(i => new ExtractedRequirement
                    {
                        Id = $"FR-{i:D3}",
                        Key = $"feature.sub{i}",
                        Description = $"Requirement {i} description",
                        IsTestable = true
                    }).ToList();

        // ============================================================
        // ADD AND RETRIEVE TESTS
        // ============================================================

        [Fact]
        public void AddToCache_ThenGetCached_ReturnsRequirements()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var cache = new RequirementCache();
            var requirements = CreateFakeRequirements(5);
            string docPath = "data/requirements_shopease.md";

            // ACT
            cache.AddToCache(docPath, requirements, 1000);
            var result = cache.GetCached(docPath);

            // ASSERT
            result.Should().NotBeNull();
            result!.Should().HaveCount(5);
            result[0].Id.Should().Be("FR-001");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public void GetCached_NonExistentKey_ReturnsNull()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var cache = new RequirementCache();

            // ACT
            var result = cache.GetCached("data/requirements_nonexistent.md");

            // ASSERT
            result.Should().BeNull("key was never added to cache");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public void AddToCache_SameKey_OverwritesPreviousEntry()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var cache = new RequirementCache();
            string docPath = "data/requirements_shopease.md";

            var firstRequirements = CreateFakeRequirements(3);
            var secondRequirements = CreateFakeRequirements(7);

            // ACT
            cache.AddToCache(docPath, firstRequirements, 500);
            cache.AddToCache(docPath, secondRequirements, 800);
            var result = cache.GetCached(docPath);

            // ASSERT
            result.Should().HaveCount(7,
                "second AddToCache should overwrite first entry");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        // ============================================================
        // EXPIRY TESTS
        // ============================================================

        [Fact]
        public void GetCached_ExpiredEntry_ReturnsNull()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var cache = new RequirementCache();
            var requirements = CreateFakeRequirements(3);
            string docPath = "data/requirements_shopease.md";

            cache.AddToCache(docPath, requirements, 500);

            // ACT — use maxAgeDays of 0 to force expiry
            var result = cache.GetCached(docPath, maxAgeDays: 0);

            // ASSERT
            result.Should().BeNull("entry with maxAgeDays=0 should be treated as expired");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public void GetCached_ValidEntry_ReturnsRequirements()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var cache = new RequirementCache();
            var requirements = CreateFakeRequirements(4);
            string docPath = "data/requirements_shopease.md";

            cache.AddToCache(docPath, requirements, 500);

            // ACT — use large maxAgeDays to ensure it's not expired
            var result = cache.GetCached(docPath, maxAgeDays: 365);

            // ASSERT
            result.Should().NotBeNull("entry within maxAgeDays should be returned");
            result!.Should().HaveCount(4);

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }
}
