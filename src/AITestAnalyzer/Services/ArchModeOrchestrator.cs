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
                    Console.WriteLine("\n✏️  Edit mode not yet implemented.");
                    goto case ArchPlanDecision.Accept;

                case ArchPlanDecision.Accept:
                default:
                    throw new NotImplementedException(
                        "Test case generation not yet implemented.");
            }
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
