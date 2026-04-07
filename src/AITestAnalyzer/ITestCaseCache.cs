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
    }
}
