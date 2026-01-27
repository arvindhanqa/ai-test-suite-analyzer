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

        public void DisplayProgress(int processedCount, string currentTestId)
        {
            // Calculate progress
            double percentComplete = (processedCount * 100.0) / _totalTests;

            // Estimate remaining time
            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
            double avgTimePerTest = processedCount > 0 ? elapsedTime / processedCount : 0;
            double estimatedRemaining = (_totalTests - processedCount) * avgTimePerTest;

            // Build progress bar (20 characters wide)
            int barWidth = 20;
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
