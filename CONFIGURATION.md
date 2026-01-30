# Configuration Guide

## Quick Start

1. **Get OpenAI API Key**
   - Go to https://platform.openai.com/api-keys
   - Sign up or log in
   - Click "Create new secret key"
   - Copy the key (starts with `sk-...`)

2. **Configure the Tool**
   - Copy `src/AITestAnalyzer/appsettings.json.sample` to `appsettings.json`
   - Replace `YOUR-OPENAI-API-KEY-HERE` with your actual API key
   - Update Excel file path if needed

3. **Run**
```bash
   dotnet run
```

## Configuration Options

### OpenAI Settings
```json
"OpenAI": {
  "ApiKey": "sk-...",     // Your OpenAI API key (required)
  "Model": "gpt-4o-mini"  // AI model to use (recommended)
}
```

**Models:**
- `gpt-4o-mini` - Recommended (fast, cheap, good quality)
- `gpt-4o` - More expensive, slightly better quality
- `gpt-3.5-turbo` - Cheaper but lower quality

### Excel Settings
```json
"Excel": {
  "FilePath": "../../data/test_cases.xlsx",  // Path to your Excel file
  "WorksheetIndex": 1                        // Which sheet to read (0-based)
}
```

**Worksheet Index:**
- `0` = First sheet
- `1` = Second sheet
- `2` = Third sheet
- etc.

## Troubleshooting

### "API key is missing"
- Make sure you copied `appsettings.json.sample` to `appsettings.json`
- Verify you replaced the placeholder with your actual key

### "Excel file not found"
- Check the file path in `appsettings.json`
- Use relative path from the executable location
- Example: `../../data/test_cases.xlsx`

### "Worksheet index out of range"
- Excel file must have at least (index + 1) sheets
- Check which sheet contains your test cases
- Update `WorksheetIndex` accordingly

### "OpenAI API connection failed"
- Verify your internet connection
- Check API key is correct (should start with `sk-`)
- Ensure you have credits in your OpenAI account

## Validation

The tool automatically validates your configuration on startup:

✅ API key format valid
✅ Excel file exists and is accessible
✅ Worksheet index valid
✅ OpenAI API connection successful

If any check fails, you'll see a detailed error message with fix instructions.