using FileComparer.Model;

namespace FileComparer.Comparison;

public sealed class ComparisonResult
{
    public required DataTable Input { get; init; }
    public required DataTable Output { get; init; }

    public required IReadOnlyList<string> KeyColumns { get; init; }
    public required IReadOnlyList<string> ComparedColumns { get; init; }
    public required IReadOnlyList<string> ColumnsOnlyInInput { get; init; }
    public required IReadOnlyList<string> ColumnsOnlyInOutput { get; init; }

    public int MatchedRowCount { get; init; }
    public required IReadOnlyList<RowMismatch> ValueMismatches { get; init; }
    public required IReadOnlyList<KeyedRow> MissingInOutput { get; init; }
    public required IReadOnlyList<KeyedRow> ExtraInOutput { get; init; }
    public required IReadOnlyList<string> DuplicateKeyWarnings { get; init; }

    public int InputRowCount => Input.Rows.Count;
    public int OutputRowCount => Output.Rows.Count;
    public int NonMatchingRowCount => ValueMismatches.Count + MissingInOutput.Count + ExtraInOutput.Count;
    public bool IsMatch => NonMatchingRowCount == 0;
}

/// <summary>A row together with the key built from its key-column values.</summary>
public sealed record KeyedRow(string DisplayKey, DataRow Row);

/// <summary>Rows that share a key but disagree on one or more compared columns.</summary>
public sealed record RowMismatch(string DisplayKey, DataRow InputRow, DataRow OutputRow, IReadOnlyList<ValueDifference> Differences);

public sealed record ValueDifference(string Column, string InputValue, string OutputValue);
