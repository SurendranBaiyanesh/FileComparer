using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileComparer.Configuration;

public sealed class ComparisonOptions
{
    public string InputFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;

    /// <summary>Columns whose values identify a row, e.g. ["PersonNumber"] or ["Name", "PersonNumber"].</summary>
    public List<string> KeyColumns { get; set; } = [];

    /// <summary>Columns to compare once rows are paired. Empty means every column the two files share.</summary>
    public List<string> CompareColumns { get; set; } = [];

    public bool ShowNonMatchingRows { get; set; } = true;

    /// <summary>Cap on rows listed per non-matching category; 0 lists them all.</summary>
    public int MaxNonMatchingRowsToShow { get; set; }

    public bool IgnoreCase { get; set; }
    public bool TrimValues { get; set; } = true;

    /// <summary>Delimiter for text files. Empty means detect it from the header line.</summary>
    public string Delimiter { get; set; } = string.Empty;

    /// <summary>Worksheet to read from spreadsheet files. Empty means the first sheet.</summary>
    public string SheetName { get; set; } = string.Empty;

    public static ComparisonOptions LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new ComparisonOptions();

        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
        return settings?.FileComparer ?? new ComparisonOptions();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed class AppSettings
    {
        [JsonPropertyName("FileComparer")]
        public ComparisonOptions? FileComparer { get; set; }
    }
}
