using System.Text.Json;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer
{
    /// <summary>
    /// Extracts structured requirements from any document format using AI.
    /// Uses semantic analysis (Topic/Subtopic/Action) instead of ID patterns.
    /// </summary>
    public class RequirementExtractor
    {
        private readonly OpenAIService _openAiService;
        private readonly string _model;

        public RequirementExtractor(Configuration config, PromptConfig promptConfig)
        {
            _openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = config.ApiKey
            });
            _model = promptConfig.Model;
        }

        /// <summary>
        /// Extract requirements from document text using AI (with caching)
        /// </summary>
        public async Task<List<ExtractedRequirement>> ExtractRequirements(
            string documentPath,
            RequirementCache cache,
            int maxAgeDays = 30)
        {
            // Try cache first
            var cached = cache.GetCached(documentPath, maxAgeDays);
            if (cached != null)
            {
                return cached;
            }

            // Not cached - read and extract
            var documentText = ReadRequirementDocument(documentPath);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("🔍 Extracting requirements using AI...");
            Console.ResetColor();

            var prompt = BuildExtractionPrompt(documentText);

            try
            {
                var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                    ChatMessage.FromSystem("You are a requirement analysis expert. Extract structured requirements from documents."),
                    ChatMessage.FromUser(prompt)
                        },
                        Model = _model,
                        MaxTokens = 2000,
                        Temperature = 0
                    });

                if (!completionResult.Successful)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ API Error: {completionResult.Error?.Message ?? "Unknown error"}");
                    Console.ResetColor();
                    return new List<ExtractedRequirement>();
                }

                string response = completionResult.Choices.First().Message.Content!.Trim();
                int tokens = completionResult.Usage!.TotalTokens;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"📊 Tokens used: {tokens:N0}");
                Console.ResetColor();

                var requirements = ParseRequirementsFromResponse(response);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Extracted {requirements.Count} requirements");
                Console.ResetColor();

                // Cache the results
                cache.AddToCache(documentPath, requirements, tokens);

                return requirements;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Extraction failed: {ex.Message}");
                Console.ResetColor();
                return new List<ExtractedRequirement>();
            }
        }

        // Keep the old ReadRequirementDocument as public helper

        /// <summary>
        /// Build the AI prompt for requirement extraction
        /// Uses "tweet style" for token optimization
        /// </summary>
        private string BuildExtractionPrompt(string documentText)
        {
            return $@"Extract testable requirements from this document. Return ONLY a JSON array, no markdown formatting, no explanation.

Each requirement should have:
- topic: High-level feature area (e.g., ""Task Management"", ""Dashboard"")
- subtopic: Specific functionality (e.g., ""Create Task"", ""Update Task"")
- expectedAction: What the system does (clear, testable behavior)
- isTestable: true/false

Rules:
1. One requirement per distinct user action
2. Focus on functional requirements only
3. Skip non-functional requirements (performance, design guidelines)
4. Be specific and actionable

Return format (JSON array only):
[
  {{
    ""topic"": ""Task Management"",
    ""subtopic"": ""Create Task"",
    ""expectedAction"": ""System shall allow users to create a new task with title, description, due date, and assignee"",
    ""isTestable"": true
  }}
]

Document to analyze:
{documentText}

JSON array:";
        }

        /// <summary>
        /// Parse AI response into ExtractedRequirement objects
        /// </summary>
        private List<ExtractedRequirement> ParseRequirementsFromResponse(string response)
        {
            try
            {
                // Clean up response (remove markdown if present)
                var jsonText = CleanJsonResponse(response);

                // Show what we're trying to parse (for debugging)
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"\n📝 Parsing JSON response ({jsonText.Length} characters)...");
                Console.ResetColor();

                // Parse JSON response
                // TODO: Add retry logic for intermittent JSON parsing errors (see issue Status: Open.#6)
                var requirements = JsonSerializer.Deserialize<List<ExtractedRequirement>>(
                    jsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                return requirements ?? new List<ExtractedRequirement>();
            }
            catch (JsonException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ JSON parse error: {ex.Message}");
                Console.WriteLine($"\nFirst 500 chars of response:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(response.Substring(0, Math.Min(500, response.Length)));
                Console.ResetColor();
                return new List<ExtractedRequirement>();
            }
        }

        /// <summary>
        /// Remove markdown code blocks and other formatting from AI response
        /// </summary>
        private string CleanJsonResponse(string response)
        {
            response = response.Trim();

            // Remove ```json and ``` markers
            if (response.StartsWith("```json"))
                response = response.Substring("```json".Length);
            else if (response.StartsWith("```"))
                response = response.Substring("```".Length);

            if (response.EndsWith("```"))
                response = response.Substring(0, response.Length - 3);

            return response.Trim();
        }

        /// <summary>
        /// Read requirement document from file
        /// </summary>
        public string ReadRequirementDocument(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Requirement document not found: {filePath}");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"📄 Reading: {Path.GetFileName(filePath)}");
            Console.ResetColor();

            return File.ReadAllText(filePath);
        }
    }
}
