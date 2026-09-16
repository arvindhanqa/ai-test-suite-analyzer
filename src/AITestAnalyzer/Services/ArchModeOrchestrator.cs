using AITestAnalyzer.Config;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public class ArchModeOrchestrator
    {
        private readonly IAIAnalyzer _aiAnalyzer;
        private readonly Configuration _config;
        private readonly PromptConfig _promptConfig;
        private readonly ITestCaseCache? _cache;

        /// <summary>
        /// Initialises a new instance of <see cref="ArchModeOrchestrator"/> with
        /// the services required to run the full ARCH Mode pipeline.
        /// </summary>
        /// <param name="aiAnalyzer">
        /// AI service used for document structure analysis and test case generation.
        /// </param>
        /// <param name="config">
        /// Application configuration containing API key and run settings.
        /// </param>
        /// <param name="promptConfig">
        /// Prompt templates and model settings for all ARCH Mode AI calls.
        /// </param>
        /// <param name="cache">
        /// Optional cache for storing and retrieving generated test cases.
        /// Pass null to run without caching.
        /// </param>
        public ArchModeOrchestrator(
            IAIAnalyzer aiAnalyzer,
            Configuration config,
            PromptConfig promptConfig,
            ITestCaseCache? cache = null)
        {
            _aiAnalyzer = aiAnalyzer;
            _config = config;
            _promptConfig = promptConfig;
            _cache = cache;
        }

        /// <summary>
        /// Runs the full ARCH Mode pipeline:
        /// analyse structure → display plan → user approval →
        /// generate section tests → generate integration tests →
        /// QA scoring → Excel + JSON output.
        /// </summary>
        /// <param name="requirementsMarkdown">
        /// Full requirements document in Markdown format.
        /// </param>
        /// <param name="requirementsSource">
        /// File path or label used in output metadata.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// An <see cref="ArchModeResult"/> containing the approved plan,
        /// all generated test cases, and run statistics.
        /// </returns>
        public async Task<ArchModeResult> RunAsync(string requirementsMarkdown, string requirementsSource, CancellationToken cancellationToken = default)
        {
            // Step 1 — Analyse document structure
            Console.WriteLine("\n🔍 Analysing requirements document structure...");
            var plan = await _aiAnalyzer.AnalyzeDocumentStructureAsync(requirementsMarkdown, cancellationToken);
            throw new NotImplementedException();
        }
    }
}
