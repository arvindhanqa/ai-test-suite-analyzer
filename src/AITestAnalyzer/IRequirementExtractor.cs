using AITestAnalyzer.Models;

namespace AITestAnalyzer
{
    public interface IRequirementExtractor
    {
        Task<List<ExtractedRequirement>> ExtractRequirementsAsync(
            string documentPath,
            RequirementCache cache,
            int maxAgeDays = Constants.CACHE_MAX_AGE_DAYS);
        string ReadRequirementDocument(string filePath);
    }
}
