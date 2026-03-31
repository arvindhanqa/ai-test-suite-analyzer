using Xunit;
using FluentAssertions;

namespace AITestAnalyzer.IntegrationTests
{
    public class PipelineTests
    {
        [Fact]
        public void TestData_SampleExcel_Exists()
        {
            // ARRANGE
            string testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "TestData",
                "test_cases_shopease.xlsx");

            // ASSERT
            File.Exists(testDataPath).Should().BeTrue(
                "because sample Excel file must exist in TestData folder");
        }
    }
}
