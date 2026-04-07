using System;
using System.Collections.Generic;
using System.Text;
using AITestAnalyzer.Models;

namespace AITestAnalyzer
{
    public interface IExcelReader
    {
        int CountTestRows();
        TestCase? ReadTestCase(int rowNumber);
        List<TestCase> ReadAllTestCases(int limit = 0);
        (bool isValid, string? errorMessage) ValidateExcelStructure();
    }
}
