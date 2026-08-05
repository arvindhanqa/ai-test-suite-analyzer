using AITestAnalyzer.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class RetryHelperTests
    {
        // ============================================================
        // SUCCESS TESTS
        // ============================================================

        [Fact]
        public async Task ExecuteWithRetry_SuccessOnFirstAttempt_ReturnsResult()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => { callCount++; return Task.FromResult("success"); },
                isSuccess: r => r == "success",
                getErrorMessage: r => "error",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            result.Should().Be("success");
            callCount.Should().Be(1, "should succeed on first attempt");
        }

        [Fact]
        public async Task ExecuteWithRetry_SuccessOnSecondAttempt_ReturnsResult()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () =>
                {
                    callCount++;
                    return Task.FromResult(callCount == 1 ? "fail" : "success");
                },
                isSuccess: r => r == "success",
                getErrorMessage: r => "not success yet",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            result.Should().Be("success");
            callCount.Should().Be(2, "should retry once then succeed");
        }

        // ============================================================
        // EXHAUSTION TESTS
        // ============================================================

        [Fact]
        public async Task ExecuteWithRetry_AllAttemptsFail_ReturnsNull()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => { callCount++; return Task.FromResult("fail"); },
                isSuccess: r => false,
                getErrorMessage: r => "always fails",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            result.Should().BeNull("all retries exhausted should return null");
            callCount.Should().Be(3, "should attempt exactly maxRetries times");
        }

        // ============================================================
        // NON-TRANSIENT EXCEPTION TESTS
        // ============================================================

        [Fact]
        public async Task ExecuteWithRetry_ArgumentException_RethrowsImmediately()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            Func<Task> act = async () => await RetryHelper.ExecuteWithRetryAsync(
                operation: () =>
                {
                    callCount++;
                    throw new ArgumentException("bad argument");
#pragma warning disable CS0162
                    return Task.FromResult("unreachable");
#pragma warning restore CS0162
                },
                isSuccess: r => true,
                getErrorMessage: r => "error",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("bad argument");
            callCount.Should().Be(1, "non-transient exception should not be retried");
        }

        [Fact]
        public async Task ExecuteWithRetry_OperationCanceledException_RethrowsImmediately()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            Func<Task> act = async () => await RetryHelper.ExecuteWithRetryAsync(
                operation: () =>
                {
                    callCount++;
                    throw new OperationCanceledException("cancelled");
#pragma warning disable CS0162
                    return Task.FromResult("unreachable");
#pragma warning restore CS0162
                },
                isSuccess: r => true,
                getErrorMessage: r => "error",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            await act.Should().ThrowAsync<OperationCanceledException>();
            callCount.Should().Be(1, "OperationCanceledException should not be retried");
        }

        [Fact]
        public async Task ExecuteWithRetry_InvalidOperationException_RethrowsImmediately()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            Func<Task> act = async () => await RetryHelper.ExecuteWithRetryAsync(
                operation: () =>
                {
                    callCount++;
                    throw new InvalidOperationException("invalid operation");
#pragma warning disable CS0162
                    return Task.FromResult("unreachable");
#pragma warning restore CS0162
                },
                isSuccess: r => true,
                getErrorMessage: r => "error",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            await act.Should().ThrowAsync<InvalidOperationException>();
            callCount.Should().Be(1, "InvalidOperationException should not be retried");
        }

        // ============================================================
        // TRANSIENT EXCEPTION TESTS
        // ============================================================

        [Fact]
        public async Task ExecuteWithRetry_TransientException_RetriesAndReturnsNull()
        {
            // ARRANGE
            var callCount = 0;

            // ACT
            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () =>
                {
                    callCount++;
                    throw new HttpRequestException("network error");
#pragma warning disable CS0162
                    return Task.FromResult("unreachable");
#pragma warning restore CS0162
                },
                isSuccess: r => true,
                getErrorMessage: r => "error",
                maxRetries: 3,
                initialDelayMs: 1);

            // ASSERT
            result.Should().BeNull("transient exception should exhaust retries and return null");
            callCount.Should().Be(3, "transient exception should retry maxRetries times");
        }
    }
}
