using Xunit;
using FluentAssertions;
using AITestAnalyzer;

namespace AITestAnalyzer.Tests
{
    public class TestCaseCacheTests
    {
        [Fact]
        public void GenerateHash_SameContent_ReturnsSameHash()
        {
            // ARRANGE
            var cache = new TestCaseCache();  // ← CREATE INSTANCE!
            
            var testCase1 = new TestCase
            {
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username\n2. Enter password",
                ExpectedResult = "User logged in"
            };

            var testCase2 = new TestCase
            {
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username\n2. Enter password",
                ExpectedResult = "User logged in"
            };

            // ACT
            string hash1 = cache.GenerateHash(testCase1);  // ← Call on instance
            string hash2 = cache.GenerateHash(testCase2);  // ← Call on instance

            // ASSERT
            hash1.Should().Be(hash2);
        }

        [Fact]
        public void GenerateHash_DifferentContent_ReturnsDifferentHash()
        {
            // ARRANGE
            var cache = new TestCaseCache();
            
            var testCase1 = new TestCase
            {
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username",
                ExpectedResult = "User logged in"
            };

            var testCase2 = new TestCase
            {
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username\n2. Enter password\n3. Click login",  // Different!
                ExpectedResult = "User logged in"
            };

            // ACT
            string hash1 = cache.GenerateHash(testCase1);
            string hash2 = cache.GenerateHash(testCase2);

            // ASSERT
            hash1.Should().NotBe(hash2, "because steps are different");
        }

        [Fact]
        public void GenerateHash_ReturnsValidSHA256Hash()
        {
            // ARRANGE
            var cache = new TestCaseCache();
            var testCase = new TestCase
            {
                Feature = "Test",
                Scenario = "Test scenario",
                Steps = "Test steps",
                ExpectedResult = "Test result"
            };

            // ACT
            string hash = cache.GenerateHash(testCase);

            // ASSERT
            hash.Should().NotBeNullOrEmpty();
            hash.Length.Should().Be(Constants.HASH_LENGTH, "because SHA256 produces 64-character hex string");
        }
    }
}
