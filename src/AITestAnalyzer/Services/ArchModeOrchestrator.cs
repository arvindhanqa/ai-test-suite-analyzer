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
        public Task<ArchModeResult> RunAsync(
            string requirementsMarkdown,
            string requirementsSource,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
