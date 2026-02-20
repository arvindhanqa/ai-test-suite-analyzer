namespace AITestAnalyzer
{
    public static class Constants
    {
        // API Configuration
        public const int MAX_RETRIES = 3;
        public const int RETRY_DELAY_MS = 1000;

        // Token Limits
        public const int TOKENS_QA_MODE = 250;
        public const int TOKENS_BA_MODE = 1000;
        public const int TOKENS_REQUIREMENT_EXTRACTION = 4000;

        // Cache Configuration
        public const int CACHE_MAX_AGE_DAYS = 30;

        // UI Configuration
        public const int PROGRESS_BAR_WIDTH = 20;

        //Hash Lenght
        public const int HASH_LENGTH = 16;
    }
}
