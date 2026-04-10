using AITestAnalyzer.Config;

namespace AITestAnalyzer.Infrastructure
{
    /// <summary>
    /// Reusable retry logic with exponential backoff for API calls.
    /// Extracted from AIAnalyzer to eliminate duplicate retry patterns.
    /// </summary>
    public static class RetryHelper
    {
        public static async Task<T?> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            Func<T, bool> isSuccess,
            Func<T, string> getErrorMessage,
            int maxRetries = Constants.MAX_RETRIES,
            int initialDelayMs = Constants.RETRY_DELAY_MS) where T : class
        {
            int retryDelayMs = initialDelayMs;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var result = await operation();

                    if (isSuccess(result))
                        return result;

                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"      ⚠️  API error (attempt {attempt}/{maxRetries}): {getErrorMessage(result)}");
                        Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"      ⚠️  Exception (attempt {attempt}/{maxRetries}): {ex.Message}");
                        Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                    }
                }
            }

            return null; // All retries exhausted
        }
    }
}
