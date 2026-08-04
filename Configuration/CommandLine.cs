namespace FileComparer.Configuration;

/// <summary>Applies command-line switches on top of the options loaded from appsettings.json.</summary>
public static class CommandLine
{
    public static bool IsHelpRequested(string[] args) =>
        args.Any(a => a is "--help" or "-h" or "/?" or "-?");

    public static string GetConfigPath(string[] args, string defaultPath)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (Matches(args[i], "--config"))
                return args[i + 1];

        return defaultPath;
    }

    public static void ApplyTo(ComparisonOptions options, string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;

            if (Matches(arg, "--input", "-i")) options.InputFilePath = Next() ?? options.InputFilePath;
            else if (Matches(arg, "--output", "-o")) options.OutputFilePath = Next() ?? options.OutputFilePath;
            else if (Matches(arg, "--columns", "-c")) options.KeyColumns = SplitList(Next());
            else if (Matches(arg, "--compare-columns")) options.CompareColumns = SplitList(Next());
            else if (Matches(arg, "--show-non-matching", "-s")) options.ShowNonMatchingRows = ParseBool(Next());
            else if (Matches(arg, "--max-rows")) options.MaxNonMatchingRowsToShow = ParseInt(Next(), options.MaxNonMatchingRowsToShow);
            else if (Matches(arg, "--ignore-case")) options.IgnoreCase = ParseBool(Next());
            else if (Matches(arg, "--trim")) options.TrimValues = ParseBool(Next());
            else if (Matches(arg, "--delimiter", "-d")) options.Delimiter = Next() ?? options.Delimiter;
            else if (Matches(arg, "--sheet")) options.SheetName = Next() ?? options.SheetName;
            else if (Matches(arg, "--config")) Next();
            else if (arg.StartsWith('-')) throw new ArgumentException($"Unknown option '{arg}'. Run with --help to see the supported options.");
        }
    }

    private static bool Matches(string arg, params string[] names) =>
        names.Any(n => string.Equals(arg, n, StringComparison.OrdinalIgnoreCase));

    private static List<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>A bare flag (no value following) counts as "true".</summary>
    private static bool ParseBool(string? value) =>
        value is null || value.StartsWith('-') || !bool.TryParse(value, out var parsed) || parsed;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;

    public static string HelpText =>
        """
        FileComparer - compares two data files row by row using one or more key columns.

        Usage:
          FileComparer [options]

        Options:
          -i, --input <path>            Input file path.
          -o, --output <path>           Output file path.
          -c, --columns <list>          Key column(s), comma separated. e.g. PersonNumber
                                        or "Name,PersonNumber".
              --compare-columns <list>  Columns to compare once rows are paired.
                                        Default: every column the two files share.
          -s, --show-non-matching <b>   Show the non-matching rows (true/false).
              --max-rows <n>            Max rows listed per category. 0 = all.
              --ignore-case <bool>      Compare values case-insensitively.
              --trim <bool>             Trim values before comparing. Default true.
          -d, --delimiter <char>        Delimiter for text files. Default: auto-detect.
              --sheet <name>            Worksheet name for .xlsx files. Default: first sheet.
              --config <path>           Settings file. Default: appsettings.json.
          -h, --help                    Show this help.

        Supported formats: .csv .txt .tsv .psv (delimited), .xml, .json, .xlsx

        Anything not supplied on the command line is read from appsettings.json, and
        anything still missing is prompted for.

        Exit codes: 0 = files match, 1 = differences found, 2 = error.
        """;
}
