using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Readers;

public interface ITableReader
{
    string FormatName { get; }

    bool CanRead(string path);

    DataTable Read(string path, ComparisonOptions options);
}

/// <summary>Turns the parsed cells of a file into a <see cref="DataTable"/>, applying the rules that are
/// shared by every format: the first record is the header, trailing empty header cells are dropped
/// (a header such as "A;B;C;" yields three columns), and ragged rows are squared off.</summary>
public static class TableBuilder
{
    public static DataTable Build(string path, string formatName, IReadOnlyList<string> header, List<(int LineNumber, List<string> Values)> records)
    {
        var columns = TrimTrailingEmpty(header);
        if (columns.Count == 0)
            throw new InvalidDataException($"No column headers found in '{path}'.");

        var width = Math.Max(columns.Count, records.Count == 0 ? 0 : records.Max(r => TrimTrailingEmpty(r.Values).Count));
        for (var i = columns.Count; i < width; i++)
            columns.Add($"Column{i + 1}");

        EnsureUniqueNames(columns);

        var rows = new List<DataRow>(records.Count);
        foreach (var (lineNumber, values) in records)
        {
            var padded = new string[width];
            for (var i = 0; i < width; i++)
                padded[i] = i < values.Count ? values[i] : string.Empty;

            rows.Add(new DataRow(lineNumber, padded));
        }

        return new DataTable(path, formatName, columns, rows);
    }

    private static List<string> TrimTrailingEmpty(IReadOnlyList<string> values)
    {
        var last = values.Count - 1;
        while (last >= 0 && string.IsNullOrWhiteSpace(values[last]))
            last--;

        return [.. values.Take(last + 1).Select(v => v ?? string.Empty)];
    }

    private static void EnsureUniqueNames(List<string> columns)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            var name = columns[i].Trim();
            if (name.Length == 0)
                name = $"Column{i + 1}";

            var candidate = name;
            var suffix = 2;
            while (!seen.Add(candidate))
                candidate = $"{name}_{suffix++}";

            columns[i] = candidate;
        }
    }
}
