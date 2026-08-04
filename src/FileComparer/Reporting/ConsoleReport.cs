using FileComparer.Comparison;
using FileComparer.Configuration;

namespace FileComparer.Reporting;

public static class ConsoleReport
{
    public static void Write(ComparisonResult result, ComparisonOptions options)
    {
        WriteHeader(result);
        WriteCounts(result);
        WriteWarnings(result);

        if (options.ShowNonMatchingRows)
            WriteNonMatchingRows(result, options.MaxNonMatchingRowsToShow);
        else if (!result.IsMatch)
            WriteLine(ConsoleColor.DarkGray, "Set ShowNonMatchingRows to true (or pass --show-non-matching true) to list the differing rows.");

        WriteVerdict(result);
    }

    private static void WriteHeader(ComparisonResult result)
    {
        WriteRule();
        WriteLine(ConsoleColor.White, "  FILE COMPARISON");
        WriteRule();
        Console.WriteLine($"  Input file    : {result.Input.SourcePath}");
        Console.WriteLine($"                  {result.Input.FormatName}, {result.InputRowCount} data row(s)");
        Console.WriteLine($"  Output file   : {result.Output.SourcePath}");
        Console.WriteLine($"                  {result.Output.FormatName}, {result.OutputRowCount} data row(s)");
        Console.WriteLine($"  Key column(s) : {string.Join(", ", result.KeyColumns)}");
        Console.WriteLine($"  Compared      : {string.Join(", ", result.ComparedColumns)}");
        Console.WriteLine();
    }

    private static void WriteCounts(ComparisonResult result)
    {
        WriteLine(ConsoleColor.White, "  COUNTS");
        WriteCount("Rows in input", result.InputRowCount, ConsoleColor.Gray);
        WriteCount("Rows in output", result.OutputRowCount, ConsoleColor.Gray);
        WriteCount("Matching rows", result.MatchedRowCount, ConsoleColor.Green);
        WriteCount("Rows with value differences", result.ValueMismatches.Count, result.ValueMismatches.Count == 0 ? ConsoleColor.Gray : ConsoleColor.Red);
        WriteCount("Rows missing in output", result.MissingInOutput.Count, result.MissingInOutput.Count == 0 ? ConsoleColor.Gray : ConsoleColor.Red);
        WriteCount("Extra rows in output", result.ExtraInOutput.Count, result.ExtraInOutput.Count == 0 ? ConsoleColor.Gray : ConsoleColor.Red);
        WriteCount("Non-matching rows (total)", result.NonMatchingRowCount, result.IsMatch ? ConsoleColor.Gray : ConsoleColor.Red);
        Console.WriteLine();
    }

    private static void WriteWarnings(ComparisonResult result)
    {
        var warnings = new List<string>();

        if (result.ColumnsOnlyInInput.Count > 0)
            warnings.Add($"Column(s) only in the input file, not compared: {string.Join(", ", result.ColumnsOnlyInInput)}");

        if (result.ColumnsOnlyInOutput.Count > 0)
            warnings.Add($"Column(s) only in the output file, not compared: {string.Join(", ", result.ColumnsOnlyInOutput)}");

        warnings.AddRange(result.DuplicateKeyWarnings);

        if (warnings.Count == 0)
            return;

        WriteLine(ConsoleColor.Yellow, "  WARNINGS");
        foreach (var warning in warnings)
            WriteLine(ConsoleColor.Yellow, $"    - {warning}");

        Console.WriteLine();
    }

    private static void WriteNonMatchingRows(ComparisonResult result, int maxRows)
    {
        if (result.IsMatch)
            return;

        WriteLine(ConsoleColor.White, "  NON-MATCHING ROWS");

        if (result.ValueMismatches.Count > 0)
        {
            WriteLine(ConsoleColor.Red, $"    Value differences ({result.ValueMismatches.Count}):");
            foreach (var mismatch in Limit(result.ValueMismatches, maxRows))
            {
                Console.WriteLine($"      [{mismatch.DisplayKey}]");
                foreach (var difference in mismatch.Differences)
                    WriteLine(ConsoleColor.Red, $"        {difference.Column}: input='{difference.InputValue}'  output='{difference.OutputValue}'");

                Console.WriteLine($"        input  (line {mismatch.InputRow.LineNumber}): {mismatch.InputRow.ToDisplayString()}");
                Console.WriteLine($"        output (line {mismatch.OutputRow.LineNumber}): {mismatch.OutputRow.ToDisplayString()}");
            }

            WriteTruncationNote(result.ValueMismatches.Count, maxRows);
        }

        WriteRowList("Present in input but missing from output", result.MissingInOutput, maxRows);
        WriteRowList("Present in output but missing from input", result.ExtraInOutput, maxRows);
        Console.WriteLine();
    }

    private static void WriteRowList(string title, IReadOnlyList<KeyedRow> rows, int maxRows)
    {
        if (rows.Count == 0)
            return;

        WriteLine(ConsoleColor.Red, $"    {title} ({rows.Count}):");
        foreach (var row in Limit(rows, maxRows))
            Console.WriteLine($"      [{row.DisplayKey}] line {row.Row.LineNumber}: {row.Row.ToDisplayString()}");

        WriteTruncationNote(rows.Count, maxRows);
    }

    private static void WriteVerdict(ComparisonResult result)
    {
        WriteRule();
        if (result.IsMatch)
            WriteLine(ConsoleColor.Green, $"  RESULT: SUCCESS - all {result.MatchedRowCount} row(s) match.");
        else
            WriteLine(ConsoleColor.Red, $"  RESULT: FAILED - {result.NonMatchingRowCount} non-matching row(s).");

        WriteRule();
    }

    private static IEnumerable<T> Limit<T>(IReadOnlyList<T> items, int maxRows) =>
        maxRows > 0 ? items.Take(maxRows) : items;

    private static void WriteTruncationNote(int total, int maxRows)
    {
        if (maxRows > 0 && total > maxRows)
            WriteLine(ConsoleColor.DarkGray, $"      ... {total - maxRows} more (raise MaxNonMatchingRowsToShow to see them all)");
    }

    private static void WriteCount(string label, int value, ConsoleColor color) =>
        WriteLine(color, $"    {label,-30}: {value}");

    private static void WriteRule() =>
        WriteLine(ConsoleColor.DarkGray, new string('=', 78));

    private static void WriteLine(ConsoleColor color, string text)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
