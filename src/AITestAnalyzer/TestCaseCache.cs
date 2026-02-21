using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;  // ADD THIS LINE
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AITestAnalyzer
{
    public class CachedResult
    {
        public string TestId { get; set; } = "";
        public string Hash { get; set; } = "";
        public string AnalysisResult { get; set; } = "";  // Keep for backward compatibility
        public string Quality { get; set; } = "";          // NEW: Quality feedback
        public string Coverage { get; set; } = "";         // NEW: Coverage info
        public int Tokens { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class TestCaseCache
    {
        private readonly string _cacheFilePath;
        private Dictionary<string, CachedResult> _cache;
        private const int DEFAULT_MAX_AGE_DAYS = Constants.CACHE_MAX_AGE_DAYS;
        private const int MAX_CACHE_ENTRIES = 1000;

        public TestCaseCache(string cacheDirectory = "cache")
        {
            // Create cache directory if it doesn't exist
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            _cacheFilePath = Path.Combine(cacheDirectory, "test_analysis_cache.json");
            _cache = new Dictionary<string, CachedResult>(); // Initialize before LoadCache
            LoadCache();
            // Check if migration is needed
            MigrateCacheIfNeeded();
            SaveCache();
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

                    // ✅ FIX: Handle null deserialization result
                    if (cacheList == null)
                    {
                        _cache = new Dictionary<string, CachedResult>();
                        return;
                    }

                    _cache = new Dictionary<string, CachedResult>();

                    foreach (var item in cacheList)
                    {
                        // ✅ FIX: Null check before using item
                        if (item != null && !string.IsNullOrEmpty(item.Hash))
                        {
                            _cache[item.Hash] = item;
                        }
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

        // Migrate old cache entries to new format
        private void MigrateCacheIfNeeded()
        {
            int migrated = 0;
            foreach (var entry in _cache.Values)
            {
                if (string.IsNullOrEmpty(entry.Quality) && !string.IsNullOrEmpty(entry.AnalysisResult))
                {
                    entry.Quality = entry.AnalysisResult;
                    entry.Coverage = "None";
                    migrated++;
                }
            }
            Console.WriteLine($"✅ Migrated {migrated} entries");
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


        /// <summary>
        /// Generates a content-based hash for cache deduplication and change detection
        /// </summary>
        /// <param name="testCase">Test case to hash. Only Feature, Scenario, Steps, and ExpectedResult fields are used (TestId and Priority are ignored)</param>
        /// <returns>
        /// 16-character SHA256 hash string used as cache key.
        /// Same test content always produces same hash (deterministic).
        /// Different content always produces different hash.
        /// </returns>
        /// <remarks>
        /// Uses SHA256 hashing algorithm on concatenated test case fields.
        /// 
        /// HASH FORMAT: "{Feature}|{Scenario}|{Steps}|{ExpectedResult}"
        /// Example: "User Auth|Valid login|1. Enter user...|User logged in" → hash: "a3f5b2c8d1e9f7a4"
        /// 
        /// WHY CONTENT-BASED (not ID-based):
        /// - TestId can differ across files (TC-001 vs TEST-001) for identical tests
        /// - Priority can change without affecting test quality
        /// - Only actual test content matters for determining cache validity
        /// 
        /// CROSS-FILE DEDUPLICATION:
        /// If File1 and File2 both contain "Login with valid credentials" test with
        /// identical steps, they generate the same hash → second file uses cached result
        /// (instant analysis + $0.00 API cost).
        /// 
        /// CHANGE DETECTION:
        /// Any modification to Feature, Scenario, Steps, or ExpectedResult generates
        /// new hash → automatic re-analysis with fresh AI feedback.
        /// </remarks>
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
                return builder.ToString().Substring(0, Constants.HASH_LENGTH); // First 16 chars
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached analysis result for the given test case hash
        /// </summary>
        /// <param name="hash">Content hash generated from test case fields (Feature, Scenario, Steps, ExpectedResult)</param>
        /// <param name="cachedResult">Output parameter containing cached result if found, null otherwise</param>
        /// <param name="maxAgeDays">Maximum age in days before cache entry expires (default: 30 days)</param>
        /// <returns>
        /// True if valid cached result found, false if not found or expired.
        /// When returning false, cachedResult is set to null.
        /// </returns>
        /// <remarks>
        /// Automatically removes expired cache entries when detected.
        /// Cache expiry ensures analysis stays current with evolving test quality standards.
        /// </remarks>
        public bool TryGetCached(string hash, out CachedResult? cachedResult, int maxAgeDays = DEFAULT_MAX_AGE_DAYS)
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
        public void AddToCache(string testId, string hash, string quality, string coverage, int tokens)
        {
            _cache[hash] = new CachedResult
            {
                TestId = testId,
                Hash = hash,
                AnalysisResult = quality,  // Keep for backward compatibility with old cache
                Quality = quality,          // NEW
                Coverage = coverage,        // NEW
                Tokens = tokens,
                CachedAt = DateTime.Now
            };

            if (_cache.Count > MAX_CACHE_ENTRIES)
            {
                var toRemove = _cache.Values
                    .OrderBy(c => c.CachedAt)
                    .Take(_cache.Count - MAX_CACHE_ENTRIES)
                    .Select(c => c.Hash)
                    .ToList();

                foreach (var key in toRemove)
                    _cache.Remove(key);
            }

            SaveCache();
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
