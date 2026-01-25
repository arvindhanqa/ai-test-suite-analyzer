namespace AITestAnalyzer
{
    public class PromptConfig
    {
        public int MaxTokens { get; set; }
        public string Model { get; set; }
        public double Temperature { get; set; }
        public string SystemMessage { get; set; }
        public string UserTemplate { get; set; }
    }
}
