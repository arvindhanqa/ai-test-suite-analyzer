using System;
using System.Collections.Generic;
using System.Text;

namespace AITestAnalyzer
{
    public class Configuration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o-mini";
        public string ExcelPath { get; set; } = string.Empty;
        public int WorksheetIndex { get; set; } = 0;  // Default to first sheet
    }
}
