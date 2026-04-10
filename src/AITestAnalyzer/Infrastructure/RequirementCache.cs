using System.Text.Json;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Infrastructure
{
    /// <summary>
    /// Caches extracted requirements to avoid redundant API calls.
    /// Similar to TestCaseCache but for requirement documents.
    /// </summary>
    public class RequirementCache
    {
        private readonly string _cacheFolder = "cache/requirements/";
        private readonly string _cacheFile = "requirements_cache.json";
        private Dictionary<string, CachedRequirements> _cache;
        private const int MAX_CACHE_ENTRIES = 500;

        public RequirementCache()
        {
            Directory.CreateDirectory(_cacheFolder);
            _cache = LoadCache();
        }

        /// <summary>
        /// Get cached requirements for a document, or return null if not cached
        /// </summary>
        /// <param name="documentPath">Path to the requirement document</param>
        /// <param name="maxAgeDays">Maximum age of cache entry in days</param>
        public List<ExtractedRequirement>? GetCached(string documentPath, int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS)
        {
            string cacheKey = GenerateCacheKey(documentPath);

            if (_cache.ContainsKey(cacheKey))
            {
                var cached = _cache[cacheKey];
                var age = DateTime.Now - cached.ExtractedDate;

                if (age.TotalDays <= maxAgeDays)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Using cached requirements (extracted {age.Days} days ago)");
                    Console.ResetColor();
                    return cached.Requirements;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠️  Cache expired ({age.Days} days old, max {maxAgeDays})");
                    Console.ResetColor();
                }
            }

            return null;
        }

        /// <summary>
        /// Add extracted requirements to cache
        /// </summary>
        public void AddToCache(string documentPath, List<ExtractedRequirement> requirements, int tokensUsed)
        {
            string cacheKey = GenerateCacheKey(documentPath);

            _cache[cacheKey] = new CachedRequirements
            {
                DocumentPath = documentPath,
                CacheKey = cacheKey,
                DocumentName = Path.GetFileName(documentPath),
                Requirements = requirements,
                ExtractedDate = DateTime.Now,
                TokensUsed = tokensUsed,
                RequirementCount = requirements.Count
            };

            if (_cache.Count > MAX_CACHE_ENTRIES)
            {
                var toRemove = _cache.Values
                    .OrderBy(c => c.ExtractedDate)
                    .Take(_cache.Count - MAX_CACHE_ENTRIES)
                    .Select(c => c.CacheKey)
                    .ToList();

                foreach (var key in toRemove)
                    _cache.Remove(key);
            }

            SaveCache();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Cached {requirements.Count} requirements");
            Console.ResetColor();
        }

        /// <summary>
        /// Generate cache key from document path (uses file hash)
        /// </summary>
        private string GenerateCacheKey(string documentPath)
        {
            // Use file path + last modified date as key
            var fileInfo = new FileInfo(documentPath);
            string normalizedPath = fileInfo.FullName.ToLower(); // ← always absolute, case-insensitive
            string combined = $"{normalizedPath}_{fileInfo.LastWriteTime:yyyyMMddHHmmss}";

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Load cache from disk
        /// </summary>
        private Dictionary<string, CachedRequirements> LoadCache()
        {
            string cachePath = Path.Combine(_cacheFolder, _cacheFile);

            if (!File.Exists(cachePath))
                return new Dictionary<string, CachedRequirements>();

            try
            {
                string json = File.ReadAllText(cachePath);
                var cache = JsonSerializer.Deserialize<Dictionary<string, CachedRequirements>>(json);
                return cache ?? new Dictionary<string, CachedRequirements>();
            }
            catch
            {
                return new Dictionary<string, CachedRequirements>();
            }
        }

        /// <summary>
        /// Save cache to disk
        /// </summary>
        public void SaveCache()
        {
            string cachePath = Path.Combine(_cacheFolder, _cacheFile);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_cache, options);

            File.WriteAllText(cachePath, json);
        }

        /// <summary>
        /// Clear all cached requirements
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
            SaveCache();
            Console.WriteLine("✅ Requirement cache cleared");
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int count, int totalRequirements, int totalTokens) GetStats()
        {
            int count = _cache.Count;
            int totalReqs = _cache.Values.Sum(c => c.RequirementCount);
            int totalTokens = _cache.Values.Sum(c => c.TokensUsed);
            return (count, totalReqs, totalTokens);
        }
    }

    /// <summary>
    /// Represents cached requirement extraction results
    /// </summary>
    public class CachedRequirements
    {
        public string DocumentPath { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string CacheKey { get; set; } = string.Empty;
        public List<ExtractedRequirement> Requirements { get; set; } = new();
        public DateTime ExtractedDate { get; set; }
        public int TokensUsed { get; set; }
        public int RequirementCount { get; set; }
    }
}
