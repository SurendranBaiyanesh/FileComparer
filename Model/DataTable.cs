namespace FileComparer.Model;

/// <summary>Format-independent view of a tabular file: named columns plus rows of string values.</summary>
public sealed class DataTable
{
    private readonly Dictionary<string, int> _columnIndex;

    public DataTable(string sourcePath, string formatName, IReadOnlyList<string> columns, IReadOnlyList<DataRow> rows)
    {
        SourcePath = sourcePath;
        FormatName = formatName;
        Columns = columns;
        Rows = rows;

        _columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
            _columnIndex.TryAdd(columns[i], i);
    }

    public string SourcePath { get; }
    public string FormatName { get; }
    public IReadOnlyList<string> Columns { get; }
    public IReadOnlyList<DataRow> Rows { get; }

    public bool HasColumn(string name) => _columnIndex.ContainsKey(name);

    public string GetValue(DataRow row, string column)
    {
        if (!_columnIndex.TryGetValue(column, out var index))
            return string.Empty;

        return index < row.Values.Count ? row.Values[index] : string.Empty;
    }

    /// <summary>Resolves a column name to the casing used in this file, so reports echo the file's own header.</summary>
    public string ResolveColumnName(string name) =>
        _columnIndex.TryGetValue(name, out var index) ? Columns[index] : name;
}

public sealed class DataRow(int lineNumber, IReadOnlyList<string> values)
{
    /// <summary>1-based position of the record in its source file, used to point the user at the offending row.</summary>
    public int LineNumber { get; } = lineNumber;

    public IReadOnlyList<string> Values { get; } = values;

    public string ToDisplayString() => string.Join(";", Values);
}
