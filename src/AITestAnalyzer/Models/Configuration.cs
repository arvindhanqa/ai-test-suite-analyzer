using System;
using System.Collections.Generic;
using System.Text;

namespace AITestAnalyzer.Models
{
    public class Configuration
    {
        public string ApiKey { get; set; } = string.Empty;
        public int WorksheetIndex { get; set; } = 0;  // Default to first sheet
    }
}
