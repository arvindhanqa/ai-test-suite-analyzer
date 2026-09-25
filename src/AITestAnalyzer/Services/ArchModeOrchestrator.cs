using AITestAnalyzer.Config;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public enum ArchPlanDecision
    {
        Accept,
        Edit,
        Back
    }

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
        public async Task<ArchModeResult> RunAsync(
            string requirementsMarkdown,
            string requirementsSource,
            CancellationToken cancellationToken = default)
        {
            // Step 1 — Analyse document structure
            Console.WriteLine("\n🔍 Analysing requirements document structure...");
            var plan = await _aiAnalyzer.AnalyzeDocumentStructureAsync(
                requirementsMarkdown, cancellationToken);

            // Step 2 — Display plan to user
            DisplayArchitecturePlan(plan);

            // Guard — empty plan
            if (plan.Sections.Count == 0)
            {
                Console.WriteLine(
                    "\n⚠️  No sections identified in the requirements document. " +
                    "Check the document format and try again.");
                return new ArchModeResult
                {
                    Plan = plan,
                    RequirementsSource = requirementsSource,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            // Step 3 — Get user decision
            var decision = GetUserPlanDecision();

            switch (decision)
            {
                case ArchPlanDecision.Back:
                    Console.WriteLine("\n↩ Returning to main menu.");
                    return new ArchModeResult
                    {
                        Plan = plan,
                        RequirementsSource = requirementsSource,
                        GeneratedAt = DateTime.UtcNow
                    };

                case ArchPlanDecision.Edit:
                    plan = await RunEditModeAsync(plan);
                    DisplayArchitecturePlan(plan);
                    return await RunGenerationAsync(
                        plan, requirementsMarkdown, requirementsSource, cancellationToken);

                case ArchPlanDecision.Accept:
                default:
                    return await RunGenerationAsync(
                        plan, requirementsMarkdown, requirementsSource, cancellationToken);
            }
        }

        private Task<ArchitecturePlan> RunEditModeAsync(ArchitecturePlan plan)
        {
            while (true)
            {
                Console.WriteLine("\n✏️  EDIT MODE — Select a section to adjust:");
                Console.WriteLine(new string('─', 60));

                for (int i = 0; i < plan.Sections.Count; i++)
                {
                    var s = plan.Sections[i];
                    Console.WriteLine($"  [{i + 1}] {s.SectionName,-30} " +
                                      $"{s.TotalRecommended} tests ({s.RiskLevel})");
                }

                Console.WriteLine("  [D] Done editing — proceed to generation");
                Console.WriteLine(new string('─', 60));
                Console.Write("Your choice: ");

                var input = Console.ReadLine()?.Trim().ToUpper();

                if (input == "D")
                    break;

                if (!int.TryParse(input, out var index) ||
                    index < 1 || index > plan.Sections.Count)
                {
                    Console.WriteLine("Invalid input. Enter a section number or D.");
                    continue;
                }

                var section = plan.Sections[index - 1];

                Console.WriteLine($"\nSection: {section.SectionName}");
                Console.WriteLine($"Current total: {section.TotalRecommended} tests");
                Console.WriteLine("Sub-topics:");

                foreach (var sub in section.SubTopics)
                    Console.WriteLine($"  └─ {sub.SubTopicName,-40} {sub.RecommendedTests}");

                Console.Write($"\nEnter new total test count for this section " +
                              $"(or press Enter to keep {section.TotalRecommended}): ");

                var countInput = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(countInput))
                {
                    Console.WriteLine("No change made.");
                    continue;
                }

                if (!int.TryParse(countInput, out var newCount) || newCount < 1)
                {
                    Console.WriteLine("Invalid number. No change made.");
                    continue;
                }

                var oldTotal = section.TotalRecommended;
                section.TotalRecommended = newCount;
                plan.TotalSectionTests = plan.Sections.Sum(s => s.TotalRecommended);

                Console.WriteLine(
                    $"✅ {section.SectionName} updated: " +
                    $"{oldTotal} → {newCount} tests.");
            }

            Console.WriteLine(
                $"\n📋 Final plan: {plan.TotalSectionTests} section tests + " +
                $"{plan.TotalIntegrationTests} integration tests = " +
                $"{plan.TotalSectionTests + plan.TotalIntegrationTests} total.");

            return Task.FromResult(plan);
        }

        private async Task<ArchModeResult> RunGenerationAsync( ArchitecturePlan plan, string requirementsMarkdown, string requirementsSource, CancellationToken cancellationToken)
        {
            var allSectionTests = new List<GeneratedTestCase>();
            int totalTokens = 0;
            int sectionNumber = 0;

            // Step 1 — Generate tests for each section sequentially
            foreach (var section in plan.Sections)
            {
                sectionNumber++;
                Console.WriteLine(
                    $"\n[{sectionNumber}/{plan.Sections.Count}] " +
                    $"Generating tests for: {section.SectionName}...");

                var (testCases, tokens) = await _aiAnalyzer.GenerateTestCasesForSectionAsync(
                    section, requirementsMarkdown, cancellationToken);

                totalTokens += tokens;
                allSectionTests.AddRange(testCases);

                Console.WriteLine(
                    $"  ✅ {testCases.Count} test cases generated " +
                    $"({tokens:N0} tokens)");
            }

            Console.WriteLine(
                $"\n📋 Section generation complete — " +
                $"{allSectionTests.Count} total tests across " +
                $"{plan.Sections.Count} sections.");

            // Step 2 — Generate integration tests
            Console.WriteLine("\n🔗 Generating integration tests...");

            var (integrationTests, integrationTokens) =
                await _aiAnalyzer.GenerateIntegrationTestsAsync(
                    requirementsMarkdown,
                    plan.IntegrationFlows,
                    allSectionTests,
                    cancellationToken);

            totalTokens += integrationTokens;

            Console.WriteLine(
                $"  ✅ {integrationTests.Count} integration test cases generated " +
                $"({integrationTokens:N0} tokens)");

            // Step 3 — Combine all test cases
            var allTestCases = new List<GeneratedTestCase>();
            allTestCases.AddRange(allSectionTests);
            allTestCases.AddRange(integrationTests);

            Console.WriteLine(
                $"\n✅ Generation complete — " +
                $"{allTestCases.Count} total test cases " +
                $"({totalTokens:N0} tokens used).");

            return new ArchModeResult
            {
                Plan = plan,
                AllTestCases = allTestCases,
                TotalTokens = totalTokens,
                RequirementsSource = requirementsSource,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private static ArchPlanDecision GetUserPlanDecision()
        {
            Console.WriteLine("Review the plan above and choose an option:");
            Console.WriteLine("  [A] Accept — start generating test cases");
            Console.WriteLine("  [E] Edit   — adjust section test counts");
            Console.WriteLine("  [B] Back   — return to main menu");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Your choice: ");
                var key = Console.ReadLine()?.Trim().ToUpper();

                switch (key)
                {
                    case "A":
                        return ArchPlanDecision.Accept;
                    case "E":
                        return ArchPlanDecision.Edit;
                    case "B":
                        return ArchPlanDecision.Back;
                    default:
                        Console.WriteLine("Invalid input. Please enter A, E, or B.");
                        break;
                }
            }
        }

        private static void DisplayArchitecturePlan(ArchitecturePlan plan)
        {
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         ARCH MODE — Document Analysis Complete               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

            Console.WriteLine("\nSECTION COVERAGE PLAN");
            Console.WriteLine(new string('─', 72));
            Console.WriteLine($"{"Section",-30} {"Prefix",-8} {"SubTopics",-10} {"Tests",-7} {"Risk",-6}");
            Console.WriteLine(new string('─', 72));

            foreach (var section in plan.Sections)
            {
                Console.WriteLine(
                    $"{section.SectionName,-30} {section.TestIdPrefix,-8} " +
                    $"{section.SubTopics.Count,-10} {section.TotalRecommended,-7} " +
                    $"{section.RiskLevel,-6}");

                foreach (var sub in section.SubTopics)
                    Console.WriteLine($"  └─ {sub.SubTopicName,-40} {sub.RecommendedTests}");
            }

            Console.WriteLine(new string('─', 72));
            Console.WriteLine($"Total section tests: {plan.TotalSectionTests}");

            Console.WriteLine("\nINTEGRATION FLOWS");
            Console.WriteLine(new string('─', 72));
            Console.WriteLine($"{"Flow",-35} {"Sections",-25} {"Tests",-5}");
            Console.WriteLine(new string('─', 72));

            foreach (var flow in plan.IntegrationFlows)
            {
                var sections = string.Join(", ", flow.SectionsInvolved);
                var sectionsDisplay = sections.Length > 23 ? sections[..23] + "…" : sections;
                Console.WriteLine($"{flow.FlowName,-35} {sectionsDisplay,-25} {flow.RecommendedTests,-5}");
            }

            Console.WriteLine(new string('─', 72));
            Console.WriteLine($"Total integration tests: {plan.TotalIntegrationTests}");
            Console.WriteLine(
                $"\nGRAND TOTAL: {plan.TotalSectionTests + plan.TotalIntegrationTests} tests\n");
        }
    }
}
