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
    public class RequirementExtractor : IRequirementExtractor
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
        /// Extract requirements from document text using AI (with caching and retry logic)
        /// Now uses smart compression: 8-15 word descriptions in pipe-delimited format
        /// </summary>
        public async Task<List<ExtractedRequirement>> ExtractRequirementsAsync(
            string documentPath,
            RequirementCache cache,
            int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS)
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

            var completionResult = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                ChatMessage.FromSystem("You extract requirements in ultra-compact pipe-delimited format. Use 8-15 word descriptions that preserve core meaning."),
                ChatMessage.FromUser(BuildSmartCompressionPrompt(documentText))
                        },
                        Model = _model,
                        MaxTokens = Constants.TOKENS_REQUIREMENT_EXTRACTION,
                        Temperature = 0
                    }),
                isSuccess: result => result?.Successful == true,
                getErrorMessage: result => result?.Error?.Message ?? "Unknown error"
            );

            if (completionResult == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Extraction failed after {Constants.MAX_RETRIES} attempts.");
                Console.ResetColor();
                return new List<ExtractedRequirement>();
            }

            string response = completionResult.Choices.First().Message.Content!.Trim();
            int tokens = completionResult.Usage!.TotalTokens;

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"📊 Tokens used: {tokens:N0}");
            Console.ResetColor();

            var requirements = ParsePipeDelimitedResponse(response);

            if (requirements.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ No requirements parsed from response.");
                Console.ResetColor();
                return new List<ExtractedRequirement>();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Extracted {requirements.Count} requirements");
            Console.ResetColor();

            cache.AddToCache(documentPath, requirements, tokens);
            return requirements;
        }

        /// <summary>
        /// Build AI prompt for smart compression extraction
        /// </summary>
        private string BuildSmartCompressionPrompt(string documentText)
        {
            return $@"Extract ALL requirements from this document as pipe-delimited lines.

FORMAT (exact):
ID|key|description|testable

RULES:
1. ID: Short unique code (2-10 chars)
   Examples: UA-01, TM-05, PO-12, NS-03

2. key: Dot-separated hierarchical path
   Examples: user.auth.login, task.create, production.order.routing
   Use clear, full words (not abbreviations)

3. description: 8-15 words capturing CORE requirement
   - Keep the WHAT (what happens)
   - Keep the WHERE (which screen/system if relevant)
   - Keep the WHEN (timing/sequence if important)
   - Use common abbreviations: txn, prod, GL, auth, pwd, mgmt, sys, cfg
   - Skip implementation details
   
   GOOD: ""route prod orders to new txn screen before GL posting""
   GOOD: ""users login with email and password, session expires after 30min""
   BAD: ""route orders"" (too short, lost meaning)
   BAD: ""system shall allow users to..."" (too verbose)

4. testable: Use 1 for yes, 0 for no

EXAMPLES:
UA-01|user.auth.login|users login with email and password, session timeout 30min|1
UA-02|user.auth.register|new users create account with email verification required|1
TM-01|task.create|create new task with title, description, assignee and due date|1
PO-01|production.order.routing|route prod orders to new txn screen for review before GL posting|1

Be CONCISE but keep SEMANTIC MEANING. 8-15 words per description.

Document:
{documentText}

Return ONLY pipe-delimited lines (one per line). NO markdown, NO code blocks, NO explanations.";
        }

        /// <summary>
        /// Parse pipe-delimited response into ExtractedRequirement objects
        /// </summary>
        private List<ExtractedRequirement> ParsePipeDelimitedResponse(string response)
        {
            try
            {
                // Clean up response (remove markdown if present)
                response = response.Trim();
                response = response.Replace("```", "").Trim();
                if (response.StartsWith("plaintext") || response.StartsWith("text"))
                {
                    response = response.Substring(response.IndexOf('\n') + 1).Trim();
                }

                // Validate we got pipe-delimited content
                if (!response.Contains('|'))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️  Response doesn't contain pipe-delimited data, trying JSON fallback...");
                    Console.ResetColor();
                    return ParseRequirementsFromResponse(response); // Fall back to old JSON parser
                }

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"🔍 Parsing pipe-delimited response ({response.Length} characters)...");
                Console.ResetColor();

                // Parse each line
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains('|'))
                                  .ToList();

                if (lines.Count == 0)
                {
                    throw new FormatException("No valid pipe-delimited lines found");
                }

                var requirements = new List<ExtractedRequirement>();
                int parseErrors = 0;

                foreach (var line in lines)
                {
                    try
                    {
                        var req = ExtractedRequirement.ParsePipeDelimited(line);
                        requirements.Add(req);
                    }
                    catch (Exception)
                    {
                        parseErrors++;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️  Skipped invalid line: {line.Substring(0, Math.Min(50, line.Length))}...");
                        Console.ResetColor();
                    }
                }

                if (parseErrors > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠️  Skipped {parseErrors} invalid lines");
                    Console.ResetColor();
                }

                return requirements;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Parse error: {ex.Message}");
                Console.WriteLine($"\nFirst 500 chars of response:");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(response.Substring(0, Math.Min(500, response.Length)));
                Console.ResetColor();
                return new List<ExtractedRequirement>();
            }
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
