using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;  // ADD THIS LINE
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Infrastructure
{
    public class CachedResult
    {
        public string TestId { get; set; } = "";
        public string Hash { get; set; } = "";
        public string AnalysisResult { get; set; } = "";  // Keep for backward compatibility
        public string Quality { get; set; } = "";          //Quality feedback
        public string Coverage { get; set; } = "";         // Coverage info
        public int Tokens { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class TestCaseCache : ITestCaseCache
    {
        private readonly string _cacheFilePath;
        private readonly string _genCacheFilePath;
        private Dictionary<string, CachedResult> _cache;
        private Dictionary<string, string> _genCache; // hash → serialized GenModeResult JSON
        private const int DEFAULT_MAX_AGE_DAYS = Constants.CACHE_MAX_AGE_DAYS;
        private const int MAX_CACHE_ENTRIES = 1000;
        private const int SAVE_BATCH_SIZE = 10; // Save every N additions
        private int _unsavedCount = 0;
        private readonly object _saveLock = new object();

        public TestCaseCache(string cacheDirectory = "cache")
        {
            // Create cache directory if it doesn't exist
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            _cacheFilePath = Path.Combine(cacheDirectory, "test_analysis_cache.json");
            _genCacheFilePath = Path.Combine(cacheDirectory, "gen_mode_cache.json");
            _cache = new Dictionary<string, CachedResult>();
            _genCache = LoadGenCache();
            LoadCache();
            // Check if migration is needed
            MigrateCacheIfNeeded();
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
                catch (Exception ex)
                {
                    Console.WriteLine($"[Cache] Warning: Failed to load cache from '{_cacheFilePath}': {ex.GetType().Name} — {ex.Message}. Starting with empty cache.");
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
            bool needsMigration = _cache.Values.Any(e =>
                string.IsNullOrEmpty(e.Quality) && !string.IsNullOrEmpty(e.AnalysisResult));

            if (!needsMigration)
                return;

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

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Migrated {migrated} cache entries to new format");
            Console.ResetColor();

            // Only save when migration actually happened
            SaveCache();
        }

        // Save cache to disk
        public void SaveCache()
        {
            lock (_saveLock)
            {
                var cacheList = new List<CachedResult>(_cache.Values);
                string json = JsonSerializer.Serialize(cacheList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_cacheFilePath, json);
                _unsavedCount = 0;
            }
        }

        // Save cache to disk (async)
        public async Task SaveCacheAsync()
        {
            var cacheList = new List<CachedResult>(_cache.Values);
            string json = JsonSerializer.Serialize(cacheList, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_cacheFilePath, json);
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
                Quality = quality,         
                Coverage = coverage,        
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

            // Batch saves — write every SAVE_BATCH_SIZE additions instead of every time
            _unsavedCount++;
            if (_unsavedCount >= SAVE_BATCH_SIZE)
            {
                lock (_saveLock)
                {
                    SaveCache();
                    _unsavedCount = 0;
                }
            }
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

        // ============================================================
        // GEN MODE CACHE — separate file: cache/gen_mode_cache.json
        // Key: SHA256 hash of requirementsMarkdown + targetCount + maxPasses
        // Value: serialized GenModeResult JSON with a CachedAt timestamp wrapper
        // ============================================================

        private Dictionary<string, string> LoadGenCache()
        {
            if (!File.Exists(_genCacheFilePath))
                return new Dictionary<string, string>();

            try
            {
                string json = File.ReadAllText(_genCacheFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Warning: Failed to load GEN cache: " +
                                  $"{ex.GetType().Name} — {ex.Message}. Starting fresh.");
                return new Dictionary<string, string>();
            }
        }

        private string GenerateGenHash(string requirementsMarkdown, int targetCount, int maxPasses)
        {
            string normalized = requirementsMarkdown.Trim().ReplaceLineEndings("\n");
            string content = $"{normalized}|{targetCount}|{maxPasses}";
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            var builder = new StringBuilder();
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));
            return Constants.GEN_CACHE_PREFIX + builder.ToString()[..Constants.HASH_LENGTH];
        }

        /// <summary>
        /// GEN MODE: Attempts to retrieve a cached GenModeResult.
        /// Returns false if not found or expired.
        /// </summary>
        public bool TryGetCachedGenResult(string requirementsMarkdown, int targetCount, int maxPasses,
            out GenModeResult? result, int maxAgeDays = DEFAULT_MAX_AGE_DAYS)
        {
            result = null;
            string hash = GenerateGenHash(requirementsMarkdown, targetCount, maxPasses);

            if (!_genCache.TryGetValue(hash, out string? json) || string.IsNullOrEmpty(json))
                return false;

            try
            {
                // Wrapper contains CachedAt + ResultJson
                var wrapper = JsonSerializer.Deserialize<GenCacheWrapper>(json);
                if (wrapper == null)
                    return false;

                var age = DateTime.UtcNow - wrapper.CachedAt;
                if (age.TotalDays > maxAgeDays)
                {
                    _genCache.Remove(hash);
                    return false;
                }

                result = JsonSerializer.Deserialize<GenModeResult>(wrapper.ResultJson);
                return result != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Warning: Failed to deserialize GEN cache entry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// GEN MODE: Stores a GenModeResult in cache serialized as JSON.
        /// </summary>
        public void AddGenResultToCache(string requirementsMarkdown, int targetCount, int maxPasses,
            GenModeResult result)
        {
            string hash = GenerateGenHash(requirementsMarkdown, targetCount, maxPasses);

            var wrapper = new GenCacheWrapper
            {
                CachedAt = DateTime.UtcNow,
                ResultJson = JsonSerializer.Serialize(result)
            };

            _genCache[hash] = JsonSerializer.Serialize(wrapper);

            try
            {
                string json = JsonSerializer.Serialize(_genCache, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_genCacheFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cache] Warning: Failed to save GEN cache: {ex.Message}");
            }
        }

        /// <summary>Wrapper stored in GEN cache — includes expiry metadata.</summary>
        private class GenCacheWrapper
        {
            public DateTime CachedAt { get; set; }
            public string ResultJson { get; set; } = "";
        }
    }
}
