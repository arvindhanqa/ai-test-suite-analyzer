using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    /// <summary>
    /// GEN MODE orchestrator — coordinates the Generate → Critique → Refine loop.
    /// Produces a refined list of test cases from a requirements markdown document,
    /// then auto-scores them via QA Mode.
    /// </summary>
    public class GenModeOrchestrator
    {
        private readonly IAIAnalyzer _aiAnalyzer;
        private readonly ITestCaseCache _cache;
        private readonly PromptConfig _promptConfig;

        /// <summary>
        /// Maximum number of Generate → Critique → Refine passes.
        /// Loop stops early if all critiques are KEEP before reaching MaxPasses.
        /// Default: 3.
        /// </summary>
        public int MaxPasses { get; set; } = 3;

        /// <summary>
        /// Default number of test cases to request from the GENERATE pass.
        /// Can be overridden by the caller via RunAsync() targetCount parameter.
        /// Default: 10.
        /// </summary>
        public int TargetTestCount { get; set; } = 10;

        /// <summary>
        /// Initializes a new instance of GenModeOrchestrator.
        /// </summary>
        /// <param name="aiAnalyzer">AI analyzer service for generate, critique, and refine calls.</param>
        /// <param name="cache">Test case cache for storing and retrieving GEN Mode results.</param>
        /// <param name="promptConfig">Prompt configuration containing GEN Mode model and templates.</param>
        public GenModeOrchestrator(
            IAIAnalyzer aiAnalyzer,
            ITestCaseCache cache,
            PromptConfig promptConfig)
        {
            _aiAnalyzer = aiAnalyzer;
            _cache = cache;
            _promptConfig = promptConfig;
        }

        /// <summary>
        /// Runs the full GEN Mode pipeline: Generate → Critique → Refine (up to MaxPasses),
        /// followed by auto QA Mode scoring of the final test cases.
        /// </summary>
        /// <param name="requirementsMarkdown">Requirements document to generate test cases from.</param>
        /// <param name="targetCount">Number of test cases to generate. Defaults to TargetTestCount.</param>
        /// <param name="maxPasses">Maximum refinement passes. Defaults to MaxPasses.</param>
        /// <returns>GenModeResult containing final test cases, pass statistics, and token usage.</returns>
        public Task<GenModeResult> RunAsync(
            string requirementsMarkdown,
            int targetCount = 0,
            int maxPasses = 0)
        {
            // TODO Day 106: implement generation pass
            // TODO Day 107: implement critique loop
            // TODO Day 108: implement refinement loop
            // TODO Day 110: implement auto QA Mode scoring
            throw new NotImplementedException("GenModeOrchestrator.RunAsync — implementation starts Day 106.");
        }
    }
}
