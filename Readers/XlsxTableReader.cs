using System.IO.Compression;
using System.Xml.Linq;
using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Readers;

/// <summary>Reads the first (or named) worksheet of an .xlsx workbook straight from the Open XML package,
/// so no spreadsheet library is needed.</summary>
public sealed class XlsxTableReader : ITableReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public string FormatName => "Excel workbook";

    public bool CanRead(string path) =>
        Path.GetExtension(path) is var ext &&
        (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || ext.Equals(".xlsm", StringComparison.OrdinalIgnoreCase));

    public DataTable Read(string path, ComparisonOptions options)
    {
        using var archive = ZipFile.OpenRead(path);

        var sharedStrings = ReadSharedStrings(archive);
        var (sheetName, sheetPath) = ResolveSheet(archive, options.SheetName, path);
        var sheet = LoadXml(archive, sheetPath)
                    ?? throw new InvalidDataException($"Worksheet '{sheetPath}' is missing from '{path}'.");

        var rows = sheet.Root?.Element(Main + "sheetData")?.Elements(Main + "row").ToList() ?? [];
        var cellRows = rows
            .Select(r => (LineNumber: (int)(r.Attribute("r") is { } a && int.TryParse(a.Value, out var n) ? n : 0), Values: ReadRow(r, sharedStrings)))
            .Where(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            .ToList();

        if (cellRows.Count == 0)
            throw new InvalidDataException($"Worksheet '{sheetName}' in '{path}' is empty.");

        var header = cellRows[0].Values;
        var records = cellRows.Skip(1).Select(r => (r.LineNumber, r.Values)).ToList();

        return TableBuilder.Build(path, $"{FormatName} (sheet '{sheetName}')", header, records);
    }

    private static List<string> ReadRow(XElement row, IReadOnlyList<string> sharedStrings)
    {
        var values = new List<string>();

        foreach (var cell in row.Elements(Main + "c"))
        {
            var columnIndex = ColumnIndex(cell.Attribute("r")?.Value, values.Count);
            while (values.Count < columnIndex)
                values.Add(string.Empty);

            values.Add(ReadCell(cell, sharedStrings));
        }

        return values;
    }

    private static string ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        var raw = cell.Element(Main + "v")?.Value ?? string.Empty;

        return type switch
        {
            "s" => int.TryParse(raw, out var index) && index < sharedStrings.Count ? sharedStrings[index] : string.Empty,
            "inlineStr" => JoinText(cell.Element(Main + "is")),
            "b" => raw == "1" ? "TRUE" : "FALSE",
            _ => raw
        };
    }

    /// <summary>Converts the letter part of a cell reference ("C7" -> 2). Falls back to the running position
    /// when the reference is absent.</summary>
    private static int ColumnIndex(string? cellReference, int fallback)
    {
        if (string.IsNullOrEmpty(cellReference))
            return fallback;

        var index = 0;
        foreach (var c in cellReference)
        {
            if (!char.IsLetter(c))
                break;

            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return index > 0 ? index - 1 : fallback;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var document = LoadXml(archive, "xl/sharedStrings.xml");
        return document?.Root?.Elements(Main + "si").Select(JoinText).ToList() ?? [];
    }

    /// <summary>Concatenates the text runs of a shared or inline string.</summary>
    private static string JoinText(XElement? element) =>
        element is null ? string.Empty : string.Concat(element.Descendants(Main + "t").Select(t => t.Value));

    private static (string Name, string Path) ResolveSheet(ZipArchive archive, string requestedName, string filePath)
    {
        var workbook = LoadXml(archive, "xl/workbook.xml")
                       ?? throw new InvalidDataException($"'{filePath}' is not a valid .xlsx workbook.");

        var sheets = workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").ToList() ?? [];
        if (sheets.Count == 0)
            throw new InvalidDataException($"'{filePath}' contains no worksheets.");

        var sheet = string.IsNullOrWhiteSpace(requestedName)
            ? sheets[0]
            : sheets.FirstOrDefault(s => string.Equals(s.Attribute("name")?.Value, requestedName, StringComparison.OrdinalIgnoreCase))
              ?? throw new InvalidDataException(
                  $"Worksheet '{requestedName}' not found in '{filePath}'. Available: {string.Join(", ", sheets.Select(s => s.Attribute("name")?.Value))}");

        var name = sheet.Attribute("name")?.Value ?? "Sheet1";
        var relationshipId = sheet.Attribute(Relationships + "id")?.Value;
        var target = ResolveRelationshipTarget(archive, relationshipId) ?? "xl/worksheets/sheet1.xml";

        return (name, target);
    }

    private static string? ResolveRelationshipTarget(ZipArchive archive, string? relationshipId)
    {
        if (string.IsNullOrEmpty(relationshipId))
            return null;

        var rels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = rels?.Root?.Elements(PackageRelationships + "Relationship")
            .FirstOrDefault(r => r.Attribute("Id")?.Value == relationshipId)?
            .Attribute("Target")?.Value;

        if (string.IsNullOrEmpty(target))
            return null;

        target = target.Replace('\\', '/').TrimStart('/');
        return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
    }

    private static XDocument? LoadXml(ZipArchive archive, string entryPath)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/'), entryPath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return null;

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
