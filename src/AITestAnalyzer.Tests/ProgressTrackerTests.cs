using AITestAnalyzer.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class ProgressTrackerTests
    {
        // ============================================================
        // CONSTRUCTOR TESTS
        // ============================================================

        [Fact]
        public void ProgressTracker_CanBeInstantiated_WithValidInputs()
        {
            // ARRANGE + ACT
            var tracker = new ProgressTracker(56, DateTime.Now);

            // ASSERT
            tracker.Should().NotBeNull();
        }

        // ============================================================
        // DISPLAY PROGRESS TESTS — capture console output
        // ============================================================

        [Fact]
        public void DisplayProgress_ZeroProcessed_ShowsZeroPercent()
        {
            // ARRANGE
            var tracker = new ProgressTracker(56, DateTime.Now);
            var output = CaptureConsoleOutput(() => tracker.DisplayProgress(0, "TC-001"));

            // ASSERT
            output.Should().Contain("0.0%");
            output.Should().Contain("0/56");
            output.Should().Contain("TC-001");
        }

        [Fact]
        public void DisplayProgress_HalfProcessed_ShowsFiftyPercent()
        {
            // ARRANGE
            var tracker = new ProgressTracker(56, DateTime.Now.AddSeconds(-28));
            var output = CaptureConsoleOutput(() => tracker.DisplayProgress(28, "TC-028"));

            // ASSERT
            output.Should().Contain("50.0%");
            output.Should().Contain("28/56");
        }

        [Fact]
        public void DisplayProgress_AllProcessed_ShowsHundredPercent()
        {
            // ARRANGE
            var tracker = new ProgressTracker(56, DateTime.Now.AddSeconds(-56));
            var output = CaptureConsoleOutput(() => tracker.DisplayProgress(56, "TC-056"));

            // ASSERT
            output.Should().Contain("100.0%");
            output.Should().Contain("56/56");
        }

        [Fact]
        public void DisplayProgress_ContainsProgressBar_WithBrackets()
        {
            // ARRANGE
            var tracker = new ProgressTracker(10, DateTime.Now);
            var output = CaptureConsoleOutput(() => tracker.DisplayProgress(5, "TC-005"));

            // ASSERT
            output.Should().Contain("[");
            output.Should().Contain("]");
            output.Should().Contain("ETA:");
        }

        [Fact]
        public void DisplayProgress_SingleTest_DoesNotThrow()
        {
            // ARRANGE
            var tracker = new ProgressTracker(1, DateTime.Now);

            // ACT
            Action act = () => tracker.DisplayProgress(1, "TC-001");

            // ASSERT
            act.Should().NotThrow();
        }

        [Fact]
        public void Complete_DoesNotThrow()
        {
            // ARRANGE
            var tracker = new ProgressTracker(10, DateTime.Now);

            // ACT
            Action act = () => tracker.Complete();

            // ASSERT
            act.Should().NotThrow();
        }

        // ============================================================
        // HELPER
        // ============================================================

        private static string CaptureConsoleOutput(Action action)
        {
            var original = Console.Out;
            using var writer = new System.IO.StringWriter();
            Console.SetOut(writer);
            try
            {
                action();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(original);
            }
        }
    }
}
