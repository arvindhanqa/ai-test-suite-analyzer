using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AITestAnalyzer
{
    public class CachedResult
    {
        public string TestId { get; set; }
        public string Hash { get; set; }
        public string AnalysisResult { get; set; }
        public int Tokens { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class TestCaseCache
    {
        private readonly string _cacheFilePath;
        private Dictionary<string, CachedResult> _cache;
        private const int DEFAULT_MAX_AGE_DAYS = 30;

        public TestCaseCache(string cacheDirectory = "cache")
        {
            // Create cache directory if it doesn't exist
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            _cacheFilePath = Path.Combine(cacheDirectory, "test_analysis_cache.json");
            LoadCache();
        }

        // Load cache from disk
        private void LoadCache()
        {
            if (File.Exists(_cacheFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var cacheList = JsonSerializer.Deserialize<List<CachedResult>>(json);
                    _cache = new Dictionary<string, CachedResult>();

                    foreach (var item in cacheList)
                    {
                        _cache[item.Hash] = item;
                    }
                }
                catch
                {
                    _cache = new Dictionary<string, CachedResult>();
                }
            }
            else
            {
                _cache = new Dictionary<string, CachedResult>();
            }
        }

        // Save cache to disk
        public void SaveCache()
        {
            var cacheList = new List<CachedResult>(_cache.Values);
            string json = JsonSerializer.Serialize(cacheList, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_cacheFilePath, json);
        }

        // Generate hash from test case content
        public string GenerateHash(TestCase testCase)
        {
            // Combine all relevant fields that define the test
            string content = $"{testCase.Feature}|{testCase.Scenario}|{testCase.Steps}|{testCase.ExpectedResult}";

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString().Substring(0, 16); // First 16 chars
            }
        }

        // Check if test is in cache (with expiry check)
        public bool TryGetCached(string hash, out CachedResult cachedResult, int maxAgeDays = DEFAULT_MAX_AGE_DAYS)
        {
            if (_cache.TryGetValue(hash, out cachedResult))
            {
                // Check if cache is too old
                var age = DateTime.Now - cachedResult.CachedAt;
                if (age.TotalDays > maxAgeDays)
                {
                    // Cache expired - remove it
                    _cache.Remove(hash);
                    cachedResult = null;
                    return false;
                }
                return true;
            }
            cachedResult = null;
            return false;
        }

        // Add result to cache
        public void AddToCache(string testId, string hash, string analysisResult, int tokens)
        {
            _cache[hash] = new CachedResult
            {
                TestId = testId,
                Hash = hash,
                AnalysisResult = analysisResult,
                Tokens = tokens,
                CachedAt = DateTime.Now
            };
        }

        // Get cache statistics
        public int GetCacheSize()
        {
            return _cache.Count;
        }

        // Get number of expired entries
        public int GetExpiredCount(int maxAgeDays = DEFAULT_MAX_AGE_DAYS)
        {
            int expired = 0;
            var now = DateTime.Now;
            foreach (var entry in _cache.Values)
            {
                var age = now - entry.CachedAt;
                if (age.TotalDays > maxAgeDays)
                {
                    expired++;
                }
            }
            return expired;
        }

        // Clear all cache
        public void ClearCache()
        {
            _cache.Clear();
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }

        // Remove expired entries
        public int CleanExpiredEntries(int maxAgeDays = DEFAULT_MAX_AGE_DAYS)
        {
            var now = DateTime.Now;
            var toRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                var age = now - kvp.Value.CachedAt;
                if (age.TotalDays > maxAgeDays)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _cache.Remove(key);
            }

            return toRemove.Count;
        }
    }
}