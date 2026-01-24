using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AITestAnalyzer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");

            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║  AI Test Suite Analyzer - Day 5 Complete  ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.WriteLine();

            // STEP 1: Load Configuration
            Console.WriteLine("📋 Step 1: Loading configuration...");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string apiKey = configuration["OpenAI:ApiKey"];
            string model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            string excelPath = configuration["Excel:FilePath"];

            // Validate API key
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR-ACTUAL-API-KEY-HERE")
            {
                Console.WriteLine("❌ ERROR: OpenAI API key not configured!");
                Console.WriteLine("Please update appsettings.json with your actual API key.");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"✅ Configuration loaded!");
            Console.WriteLine($"   API Key: {apiKey.Substring(0, 7)}...{apiKey.Substring(apiKey.Length - 4)}");
            Console.WriteLine($"   Model: {model}");
            Console.WriteLine();

            // STEP 2: Read Test Case from Excel
            Console.WriteLine("📊 Step 2: Reading test case from Excel...");

            if (!File.Exists(excelPath))
            {
                Console.WriteLine($"❌ ERROR: Excel file not found at: {excelPath}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            TestCase testCase = null;

            try
            {
                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                    testCase = new TestCase
                    {
                        TestId = worksheet.Cells[2, 1].Value?.ToString() ?? "",
                        Feature = worksheet.Cells[2, 2].Value?.ToString() ?? "",
                        Scenario = worksheet.Cells[2, 3].Value?.ToString() ?? "",
                        Priority = worksheet.Cells[2, 4].Value?.ToString() ?? "",
                        Steps = worksheet.Cells[2, 5].Value?.ToString() ?? "",
                        ExpectedResult = worksheet.Cells[2, 6].Value?.ToString() ?? "",
                        Status = worksheet.Cells[2, 7].Value?.ToString() ?? ""
                    };

                    Console.WriteLine($"✅ Test case loaded: {testCase.TestId}");
                    Console.WriteLine($"   Feature: {testCase.Feature}");
                    Console.WriteLine($"   Scenario: {testCase.Scenario}");
                    Console.WriteLine($"   Priority: {testCase.Priority}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excel Error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // STEP 3: Send to OpenAI for Analysis
            Console.WriteLine("🤖 Step 3: Analyzing test case with AI...");
            Console.WriteLine("   (This may take 5-10 seconds...)");
            Console.WriteLine();

            try
            {
                var openAiService = new OpenAIService(new OpenAiOptions()
                {
                    ApiKey = apiKey
                });

                var completionResult = await openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromSystem("You are an expert QA test case quality analyzer. Analyze test cases for completeness, clarity, and adherence to best practices."),
                            ChatMessage.FromUser($@"Analyze this test case for quality issues:

**Test Case Details:**
- Test ID: {testCase.TestId}
- Feature: {testCase.Feature}
- Scenario: {testCase.Scenario}
- Priority: {testCase.Priority}

**Steps:**
{testCase.Steps}

**Expected Result:**
{testCase.ExpectedResult}

**Please provide:**
1. Overall quality rating (Excellent/Good/Fair/Poor)
2. Strengths of this test case
3. Issues or weaknesses found
4. Specific recommendations for improvement
5. Missing test scenarios or edge cases

Keep your analysis concise and actionable.")
                        },
                        Model = Models.Gpt_4o_mini,
                        MaxTokens = 800,
                        Temperature = 0.3f
                    });

                if (completionResult.Successful)
                {
                    Console.WriteLine("╔════════════════════════════════════════════╗");
                    Console.WriteLine("║          AI QUALITY ANALYSIS REPORT        ║");
                    Console.WriteLine("╚════════════════════════════════════════════╝");
                    Console.WriteLine();
                    Console.WriteLine(completionResult.Choices.First().Message.Content);
                    Console.WriteLine();
                    Console.WriteLine("╔════════════════════════════════════════════╗");
                    Console.WriteLine("║              END OF ANALYSIS               ║");
                    Console.WriteLine("╚════════════════════════════════════════════╝");
                    Console.WriteLine();
                    Console.WriteLine("✅ SUCCESS! AI analysis completed!");
                    Console.WriteLine($"   Tokens used: ~{completionResult.Usage.TotalTokens}");
                    Console.WriteLine($"   Estimated cost: ~${(completionResult.Usage.TotalTokens * 0.00000015):F6}");
                }
                else
                {
                    Console.WriteLine($"❌ OpenAI API Error:");
                    Console.WriteLine($"   Code: {completionResult.Error?.Code}");
                    Console.WriteLine($"   Message: {completionResult.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ API Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner Error: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("🎉 DAY 5 COMPLETE - OPENAI INTEGRATION WORKING!");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}