namespace AITestAnalyzer
{
    public class TestCase
    {
        public string TestId { get; set; }
        public string Feature { get; set; }
        public string Scenario { get; set; }
        public string Priority { get; set; }
        public string Steps { get; set; }
        public string ExpectedResult { get; set; }
        public string Status { get; set; }

        // Constructor
        public TestCase()
        {
            TestId = string.Empty;
            Feature = string.Empty;
            Scenario = string.Empty;
            Priority = string.Empty;
            Steps = string.Empty;
            ExpectedResult = string.Empty;
            Status = string.Empty;
        }

        // NEW: Constructor with parameters
        public TestCase(
            string testId,
            string feature,
            string scenario,
            string priority,
            string steps,
            string expectedResult,
            string status)
        {
            TestId = testId;
            Feature = feature;
            Scenario = scenario;
            Priority = priority;
            Steps = steps;
            ExpectedResult = expectedResult;
            Status = status;
        }

        // ToString for easy display
        public override string ToString()
        {
            return $"[{TestId}] {Scenario} - {Priority}";
        }
    }
}