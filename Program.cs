using FileComparer.Comparison;
using FileComparer.Configuration;
using FileComparer.Readers;
using FileComparer.Reporting;

const int exitMatch = 0;
const int exitDifferences = 1;
const int exitError = 2;

if (CommandLine.IsHelpRequested(args))
{
    Console.WriteLine(CommandLine.HelpText);
    return exitMatch;
}

try
{
    var configPath = CommandLine.GetConfigPath(args, Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
    var options = ComparisonOptions.LoadFromFile(configPath);
    CommandLine.ApplyTo(options, args);
    PromptForMissingValues(options);

    var factory = new TableReaderFactory();
    var input = factory.Load(options.InputFilePath, options);
    var output = factory.Load(options.OutputFilePath, options);

    var result = new FileComparisonEngine(options).Compare(input, output);
    ConsoleReport.Write(result, options);

    return result.IsMatch ? exitMatch : exitDifferences;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Console.ResetColor();
    return exitError;
}

static void PromptForMissingValues(ComparisonOptions options)
{
    options.InputFilePath = Resolve(options.InputFilePath, "Input file path : ");
    options.OutputFilePath = Resolve(options.OutputFilePath, "Output file path: ");

    if (options.KeyColumns.Count == 0)
    {
        var answer = Ask("Column(s) to compare on (comma separated, e.g. Name,PersonNumber): ");
        options.KeyColumns = [.. answer.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    static string Resolve(string value, string prompt) =>
        string.IsNullOrWhiteSpace(value) ? Ask(prompt) : value;

    static string Ask(string prompt)
    {
        Console.Write(prompt);
        // Paths pasted from Explorer usually arrive wrapped in quotes.
        return (Console.ReadLine() ?? string.Empty).Trim().Trim('"', '\'');
    }
}
