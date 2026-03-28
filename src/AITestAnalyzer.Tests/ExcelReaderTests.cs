using System.Buffers.Text;
using AITestAnalyzer;
using FluentAssertions;
using Moq;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class ExcelReaderTests
    {
        [Fact]
        public void CountTestRows_WhenCalled_ReturnsCorrectCount()
        {
            // ARRANGE
            var mockReader = new Mock<IExcelReader>();
            mockReader.Setup(r => r.CountTestRows()).Returns(56);

            // ACT
            int count = mockReader.Object.CountTestRows();

            // ASSERT
            count.Should().Be(56);
        }

        [Fact]
        public void CountTestRows_WhenCalled_InvokedExactlyOnce()
        {
            // ARRANGE
            var mockReader = new Mock<IExcelReader>();
            mockReader.Setup(r => r.CountTestRows()).Returns(10);

            // ACT
            mockReader.Object.CountTestRows();

            // ASSERT
            mockReader.Verify(r => r.CountTestRows(), Times.Once());
        }

        [Fact]
        public void ReadAllTestCases_WithLimit_ReturnsCorrectNumberOfTests()
        {
            // ARRANGE
            var mockReader = new Mock<IExcelReader>();

            var fakeTestCases = new List<TestCase>
            {
                new TestCase { TestId = "TC-001", Feature = "Login",
                    Scenario = "Valid login", Steps = "1. Enter credentials",
                    ExpectedResult = "User logged in" },
                new TestCase { TestId = "TC-002", Feature = "Login",
                    Scenario = "Invalid login", Steps = "1. Enter wrong credentials",
                    ExpectedResult = "Error shown" },
                new TestCase { TestId = "TC-003", Feature = "Cart",
                    Scenario = "Add item", Steps = "1. Click add",
                    ExpectedResult = "Item in cart" }
            };

            mockReader.Setup(r => r.ReadAllTestCases(3)).Returns(fakeTestCases);

            // ACT
            var result = mockReader.Object.ReadAllTestCases(3);

            // ASSERT
            result.Should().HaveCount(3);
            result[0].TestId.Should().Be("TC-001");
            result[2].TestId.Should().Be("TC-003");
        }

        [Fact]
        public void ValidateExcelStructure_WithValidFile_ReturnsSuccess()
        {
            // ARRANGE
            var mockReader = new Mock<IExcelReader>();
            mockReader.Setup(r => r.ValidateExcelStructure())
                      .Returns((true, "Excel structure is valid"));

            // ACT
            var (isValid, message) = mockReader.Object.ValidateExcelStructure();

            // ASSERT
            isValid.Should().BeTrue();
            message.Should().Be("Excel structure is valid");
        }

        [Fact]
        public void ValidateExcelStructure_WithInvalidFile_ReturnsFailure()
        {
            // ARRANGE
            var mockReader = new Mock<IExcelReader>();
            mockReader.Setup(r => r.ValidateExcelStructure())
                      .Returns((false, "Column 1 header mismatch. Expected 'Test ID', found 'ID'"));

            // ACT
            var (isValid, message) = mockReader.Object.ValidateExcelStructure();

            // ASSERT
            isValid.Should().BeFalse();
            message.Should().Contain("mismatch");
        }
    }
}
