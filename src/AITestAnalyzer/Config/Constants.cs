namespace AITestAnalyzer.Config
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
        public const int ESTIMATED_TOKENS_PER_CACHED_TEST = 150;

        // Expected Excel column headers (case-insensitive match)
        public const string HeaderTestId = "Test ID";
        public const string HeaderFeature = "Feature";
        public const string HeaderScenario = "Scenario";
        public const string HeaderPriority = "Priority";
        public const string HeaderSteps = "Steps";

        // Result values
        public const string RESULT_GOOD = "GOOD";
        public const string RESULT_ERROR_PREFIX = "ERROR:";
        public const string NO_COVERAGE = "None";
    }
}
