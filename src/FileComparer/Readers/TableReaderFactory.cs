using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Readers;

/// <summary>Picks the reader for a file from its extension. Add a reader here to support another format.</summary>
public sealed class TableReaderFactory
{
    private readonly List<ITableReader> _readers =
    [
        new DelimitedTableReader(),
        new XmlTableReader(),
        new JsonTableReader(),
        new XlsxTableReader()
    ];

    public DataTable Load(string path, ComparisonOptions options)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        var reader = _readers.FirstOrDefault(r => r.CanRead(path))
                     ?? throw new NotSupportedException(
                         $"No reader for '{Path.GetExtension(path)}' files. Supported: .csv .txt .tsv .psv .xml .json .xlsx");

        return reader.Read(path, options);
    }
}
