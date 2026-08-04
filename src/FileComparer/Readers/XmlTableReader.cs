using System.Xml;
using System.Xml.Linq;
using FileComparer.Configuration;
using FileComparer.Model;

namespace FileComparer.Readers;

/// <summary>Reads XML where each record is an element and each column is an attribute or a leaf child element.</summary>
public sealed class XmlTableReader : ITableReader
{
    public string FormatName => "XML";

    public bool CanRead(string path) =>
        string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase);

    public DataTable Read(string path, ComparisonOptions options)
    {
        var root = XDocument.Load(path, LoadOptions.SetLineInfo).Root
                   ?? throw new InvalidDataException($"'{path}' has no root element.");

        var rowElements = FindRowElements(root);
        if (rowElements.Count == 0)
            throw new InvalidDataException($"No record elements found in '{path}'.");

        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cellsPerRow = new List<(int LineNumber, Dictionary<string, string> Cells)>();

        foreach (var element in rowElements)
        {
            var cells = ReadCells(element);
            foreach (var name in cells.Keys)
                if (seen.Add(name))
                    columns.Add(name);

            cellsPerRow.Add((((IXmlLineInfo)element).LineNumber, cells));
        }

        var records = cellsPerRow
            .Select(r => (r.LineNumber, columns.Select(c => r.Cells.GetValueOrDefault(c, string.Empty)).ToList()))
            .ToList();

        return TableBuilder.Build(path, FormatName, columns, records);
    }

    /// <summary>Descends through single-element wrappers such as &lt;Root&gt;&lt;Rows&gt;… until it reaches the repeated records.</summary>
    private static List<XElement> FindRowElements(XElement root)
    {
        var current = root;
        while (true)
        {
            var children = current.Elements().ToList();
            if (children.Count != 1 || !children[0].Elements().Any(c => c.HasElements || c.HasAttributes))
                return children;

            current = children[0];
        }
    }

    private static Dictionary<string, string> ReadCells(XElement element)
    {
        var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            cells[attribute.Name.LocalName] = attribute.Value;

        foreach (var child in element.Elements())
            cells[child.Name.LocalName] = child.HasElements ? child.ToString(SaveOptions.DisableFormatting) : child.Value;

        return cells;
    }
}
