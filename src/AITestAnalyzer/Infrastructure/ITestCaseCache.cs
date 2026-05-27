using AITestAnalyzer.Config;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;

namespace AITestAnalyzer
{
    public interface ITestCaseCache
    {
        string GenerateHash(TestCase testCase);
        bool TryGetCached(string hash, out CachedResult? cachedResult, int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS);
        void AddToCache(string testId, string hash, string quality, string coverage, int tokens);
        void SaveCache();
        int GetCacheSize();
        int GetExpiredCount(int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS);
        void ClearCache();
        int CleanExpiredEntries(int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS);
        Task SaveCacheAsync();

        /// <summary>
        /// GEN MODE: Attempts to retrieve a cached GenModeResult.
        /// Cache key is a hash of requirementsMarkdown + targetCount + maxPasses.
        /// </summary>
        bool TryGetCachedGenResult(string requirementsMarkdown, int targetCount, int maxPasses,
            out GenModeResult? result, int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS);

        /// <summary>
        /// GEN MODE: Stores a GenModeResult in cache.
        /// Cache key is a hash of requirementsMarkdown + targetCount + maxPasses.
        /// </summary>
        void AddGenResultToCache(string requirementsMarkdown, int targetCount, int maxPasses,
            GenModeResult result);
    }
}
