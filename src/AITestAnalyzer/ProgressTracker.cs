using System;

namespace AITestAnalyzer
{
    public class ProgressTracker
    {
        private readonly int _totalTests;
        private readonly DateTime _startTime;

        public ProgressTracker(int totalTests, DateTime startTime)
        {
            _totalTests = totalTests;
            _startTime = startTime;
        }

        /// <summary>
        /// Displays real-time progress bar with percentage, test count, and estimated time remaining
        /// </summary>
        /// <param name="processedCount">Number of tests analyzed so far (used to calculate percentage and ETA)</param>
        /// <param name="currentTestId">Test ID currently being processed (e.g., "TC-038"). Displayed in progress bar for user visibility.</param>
        /// <remarks>
        /// BEHAVIOR:
        /// - Updates single line using \r carriage return (overwrites previous progress)
        /// - Calculates percentage: (processedCount * 100.0) / totalTests
        /// - Estimates time remaining: (totalTests - processedCount) * avgTimePerTest
        /// - Formats time: "Xm Ys" if ≥1 minute, else "Xs"
        /// - Visual progress bar: [=====>.......] (20 chars wide, filled vs empty)
        /// 
        /// EXAMPLE:
        /// - processedCount=38, totalTests=56, currentTestId="TC-038"
        /// - Output: "[============>.......] 67.9% | 38/56 | TC-038 | ETA: 35s"
        /// </remarks>
        public void DisplayProgress(int processedCount, string currentTestId)
        {
            // Calculate progress
            double percentComplete = (processedCount * 100.0) / _totalTests;

            // Estimate remaining time
            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
            double avgTimePerTest = processedCount > 0 ? elapsedTime / processedCount : 0;
            double estimatedRemaining = (_totalTests - processedCount) * avgTimePerTest;

            // Build progress bar (20 characters wide)
            int barWidth = Constants.PROGRESS_BAR_WIDTH;
            int filledWidth = (int)(barWidth * percentComplete / 100);
            string progressBar = "[" + new string('=', filledWidth) + new string('.', barWidth - filledWidth) + "]";

            // Format time remaining
            TimeSpan remainingSpan = TimeSpan.FromSeconds(estimatedRemaining);
            string timeRemaining = remainingSpan.TotalMinutes >= 1
                ? $"{(int)remainingSpan.TotalMinutes}m {remainingSpan.Seconds}s"
                : $"{remainingSpan.Seconds}s";

            // Display progress on single line (overwrites previous line)
            Console.Write($"\r   {progressBar} {percentComplete:F1}% | {processedCount}/{_totalTests} | {currentTestId} | ETA: {timeRemaining}   ");
        }

        public void Complete()
        {
            Console.WriteLine(); // New line after progress bar
            Console.WriteLine("   ✅ Analysis complete!");
            Console.WriteLine();
        }
    }
}
