using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Comparison;

/// <summary>Pairs rows from the two files by their key-column values and compares the remaining columns.
/// Row order is irrelevant; only keys and values decide the outcome.</summary>
public sealed class FileComparisonEngine(ComparisonOptions options)
{
    // Unit separator: cannot occur in real data, so composite keys stay unambiguous.
    private const char KeySeparator = (char)0x1F;

    public ComparisonResult Compare(DataTable input, DataTable output)
    {
        var keyColumns = ResolveKeyColumns(input, output);
        var comparedColumns = ResolveComparedColumns(input, output, keyColumns);

        var inputGroups = GroupByKey(input, keyColumns);
        var outputGroups = GroupByKey(output, keyColumns);

        var mismatches = new List<RowMismatch>();
        var missingInOutput = new List<KeyedRow>();
        var extraInOutput = new List<KeyedRow>();
        var matched = 0;

        foreach (var (key, inputRows) in inputGroups)
        {
            if (!outputGroups.TryGetValue(key, out var outputRows))
            {
                missingInOutput.AddRange(inputRows.Select(r => new KeyedRow(DisplayKey(input, r, keyColumns), r)));
                continue;
            }

            var pairCount = Math.Min(inputRows.Count, outputRows.Count);
            for (var i = 0; i < pairCount; i++)
            {
                var differences = CompareValues(input, inputRows[i], output, outputRows[i], comparedColumns);
                if (differences.Count == 0)
                    matched++;
                else
                    mismatches.Add(new RowMismatch(DisplayKey(input, inputRows[i], keyColumns), inputRows[i], outputRows[i], differences));
            }

            // Duplicate keys: whatever is left over on either side has no counterpart.
            missingInOutput.AddRange(inputRows.Skip(pairCount).Select(r => new KeyedRow(DisplayKey(input, r, keyColumns), r)));
            extraInOutput.AddRange(outputRows.Skip(pairCount).Select(r => new KeyedRow(DisplayKey(output, r, keyColumns), r)));
        }

        foreach (var (key, outputRows) in outputGroups.Where(g => !inputGroups.ContainsKey(g.Key)))
            extraInOutput.AddRange(outputRows.Select(r => new KeyedRow(DisplayKey(output, r, keyColumns), r)));

        return new ComparisonResult
        {
            Input = input,
            Output = output,
            KeyColumns = keyColumns,
            ComparedColumns = comparedColumns,
            ColumnsOnlyInInput = [.. input.Columns.Where(c => !output.HasColumn(c))],
            ColumnsOnlyInOutput = [.. output.Columns.Where(c => !input.HasColumn(c))],
            MatchedRowCount = matched,
            ValueMismatches = mismatches,
            MissingInOutput = missingInOutput,
            ExtraInOutput = extraInOutput,
            DuplicateKeyWarnings = [.. DescribeDuplicates("Input", inputGroups), .. DescribeDuplicates("Output", outputGroups)]
        };
    }

    private List<string> ResolveKeyColumns(DataTable input, DataTable output)
    {
        if (options.KeyColumns.Count == 0)
            throw new InvalidOperationException("At least one key column is required (for example: --columns PersonNumber).");

        var missing = options.KeyColumns.Where(c => !input.HasColumn(c) || !output.HasColumn(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Column(s) not present in both files: {string.Join(", ", missing)}.{Environment.NewLine}" +
                $"  Input columns : {string.Join(", ", input.Columns)}{Environment.NewLine}" +
                $"  Output columns: {string.Join(", ", output.Columns)}");

        return [.. options.KeyColumns.Select(input.ResolveColumnName)];
    }

    private List<string> ResolveComparedColumns(DataTable input, DataTable output, List<string> keyColumns)
    {
        if (options.CompareColumns.Count > 0)
        {
            var missing = options.CompareColumns.Where(c => !input.HasColumn(c) || !output.HasColumn(c)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Compare column(s) not present in both files: {string.Join(", ", missing)}.");

            return [.. options.CompareColumns.Select(input.ResolveColumnName)];
        }

        var common = input.Columns.Where(output.HasColumn).ToList();
        var nonKey = common.Where(c => !keyColumns.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

        // With only key columns in common there is nothing left to compare, so the keys themselves are the comparison.
        return nonKey.Count > 0 ? nonKey : common;
    }

    private Dictionary<string, List<DataRow>> GroupByKey(DataTable table, List<string> keyColumns)
    {
        var groups = new Dictionary<string, List<DataRow>>(StringComparer.Ordinal);

        foreach (var row in table.Rows)
        {
            var key = string.Join(KeySeparator, keyColumns.Select(c => Normalize(table.GetValue(row, c))));
            if (!groups.TryGetValue(key, out var rows))
                groups[key] = rows = [];

            rows.Add(row);
        }

        return groups;
    }

    private List<ValueDifference> CompareValues(DataTable input, DataRow inputRow, DataTable output, DataRow outputRow, List<string> columns)
    {
        var differences = new List<ValueDifference>();

        foreach (var column in columns)
        {
            var inputValue = input.GetValue(inputRow, column);
            var outputValue = output.GetValue(outputRow, column);

            if (!string.Equals(Normalize(inputValue), Normalize(outputValue), StringComparison.Ordinal))
                differences.Add(new ValueDifference(column, inputValue, outputValue));
        }

        return differences;
    }

    private string Normalize(string value)
    {
        if (options.TrimValues)
            value = value.Trim();

        return options.IgnoreCase ? value.ToUpperInvariant() : value;
    }

    private string DisplayKey(DataTable table, DataRow row, List<string> keyColumns) =>
        string.Join(", ", keyColumns.Select(c => $"{c}={table.GetValue(row, c)}"));

    private static IEnumerable<string> DescribeDuplicates(string side, Dictionary<string, List<DataRow>> groups) =>
        groups.Where(g => g.Value.Count > 1)
            .Select(g => $"{side} file has {g.Value.Count} rows with key '{g.Key.Replace(KeySeparator, '|')}' (lines {string.Join(", ", g.Value.Select(r => r.LineNumber))}).");
}
