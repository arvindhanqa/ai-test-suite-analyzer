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
        public async Task<GenModeResult> RunAsync(
                    string requirementsMarkdown,
                    int targetCount = 0,
                    int maxPasses = 0)
        {
            int resolvedTargetCount = targetCount > 0 ? targetCount : TargetTestCount;
            int resolvedMaxPasses = maxPasses > 0 ? maxPasses : MaxPasses;

            // ── GUARD: requirements must be provided ─────────────────
            if (string.IsNullOrWhiteSpace(requirementsMarkdown))
                throw new ArgumentException(
                    "GEN Mode requires a requirements document. " +
                    "Please provide a .md or .txt requirements file.");

            var result = new GenModeResult
            {
                RequirementsSource = "provided",
                GeneratedAt = DateTime.UtcNow
            };

            // ── PASS 1: Generate initial test cases ──────────────────
            Console.WriteLine();
            Console.WriteLine($"   🔄 Pass 1 — Generating {resolvedTargetCount} test cases...");

            var (initialTestCases, generateTokens) = await _aiAnalyzer.GenerateTestCasesAsync(
                requirementsMarkdown,
                resolvedTargetCount);

            result.TestCases = initialTestCases;
            result.TotalPasses = 1;
            result.TotalTokens += generateTokens;

            Console.WriteLine($"   ✅ Pass 1 complete — {initialTestCases.Count} test cases generated " +
                              $"({generateTokens} tokens)");

            Console.WriteLine($"   ✅ Pass 1 complete — {initialTestCases.Count} test cases generated " +
                              $"({generateTokens} tokens)");

            // ── CRITIQUE after Pass 1 ────────────────────────────────
            Console.WriteLine($"   🔍 Critiquing Pass 1 output...");

            var (critiques, critiqueTokens) = await _aiAnalyzer.CritiqueTestCasesAsync(
                result.TestCases,
                requirementsMarkdown);

            result.TotalTokens += critiqueTokens;

            int keepCount = critiques.Count(c => c.Action == "KEEP");
            int reviseCount = critiques.Count(c => c.Action == "REVISE");
            int dropCount = critiques.Count(c => c.Action == "DROP");

            Console.WriteLine($"   📊 Critique summary — " +
                              $"KEEP: {keepCount}  REVISE: {reviseCount}  DROP: {dropCount} " +
                              $"({critiqueTokens} tokens)");

            // Early exit — nothing to refine
            bool needsRefinement = critiques.Any(c => c.Action == "REVISE" || c.Action == "DROP");
            if (!needsRefinement)
            {
                Console.WriteLine($"   ✅ All critiques are KEEP — no refinement needed.");
                // TODO Day 110: implement auto QA Mode scoring
                return result;
            }

            // ── REFINEMENT LOOP — up to (MaxPasses - 1) refinement passes ──
            var currentCritiques = critiques;

            for (int pass = 2; pass <= resolvedMaxPasses; pass++)
            {
                Console.WriteLine($"   🔄 Pass {pass} — Refining test cases...");

                var (refined, refineTokens) = await _aiAnalyzer.RefineTestCasesAsync(
                    result.TestCases,
                    currentCritiques,
                    requirementsMarkdown);

                result.TestCases = refined;
                result.TotalPasses = pass;
                result.TotalTokens += refineTokens;

                Console.WriteLine($"   ✅ Pass {pass} refinement complete — " +
                                  $"{refined.Count} test cases ({refineTokens} tokens)");

                // No more passes needed if we've hit the limit
                if (pass == resolvedMaxPasses)
                {
                    Console.WriteLine($"   ℹ️  Maximum passes ({resolvedMaxPasses}) reached.");
                    break;
                }

                // Critique the refined output
                Console.WriteLine($"   🔍 Critiquing Pass {pass} output...");

                var (newCritiques, newCritiqueTokens) = await _aiAnalyzer.CritiqueTestCasesAsync(
                    result.TestCases,
                    requirementsMarkdown);

                result.TotalTokens += newCritiqueTokens;

                int newKeep = newCritiques.Count(c => c.Action == "KEEP");
                int newRevise = newCritiques.Count(c => c.Action == "REVISE");
                int newDrop = newCritiques.Count(c => c.Action == "DROP");

                Console.WriteLine($"   📊 Critique summary — " +
                                  $"KEEP: {newKeep}  REVISE: {newRevise}  DROP: {newDrop} " +
                                  $"({newCritiqueTokens} tokens)");

                // Early exit — refinement converged
                bool stillNeedsWork = newCritiques.Any(c => c.Action == "REVISE" || c.Action == "DROP");
                if (!stillNeedsWork)
                {
                    Console.WriteLine($"   ✅ All critiques are KEEP — refinement converged at pass {pass}.");
                    break;
                }

                currentCritiques = newCritiques;
            }

            // TODO Day 110: implement auto QA Mode scoring

            return result;
        }
    }
}
