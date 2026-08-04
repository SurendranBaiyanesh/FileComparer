using System.Text.Json;
using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Readers;

/// <summary>Reads a JSON array of objects, or an object whose first array property holds the records.</summary>
public sealed class JsonTableReader : ITableReader
{
    public string FormatName => "JSON";

    public bool CanRead(string path) =>
        string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

    public DataTable Read(string path, ComparisonOptions options)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var array = FindRecordArray(document.RootElement)
                    ?? throw new InvalidDataException($"No array of records found in '{path}'.");

        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cellsPerRow = new List<Dictionary<string, string>>();

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"'{path}' contains a non-object record; expected an array of objects.");

            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                cells[property.Name] = ToText(property.Value);
                if (seen.Add(property.Name))
                    columns.Add(property.Name);
            }

            cellsPerRow.Add(cells);
        }

        var records = cellsPerRow
            .Select((cells, index) => (index + 1, columns.Select(c => cells.GetValueOrDefault(c, string.Empty)).ToList()))
            .ToList();

        return TableBuilder.Build(path, FormatName, columns, records);
    }

    private static JsonElement? FindRecordArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in root.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.Array)
                return property.Value;

        return null;
    }

    private static string ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
        _ => value.ToString()
    };
}
