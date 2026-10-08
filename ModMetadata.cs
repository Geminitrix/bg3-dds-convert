using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DDS_Convert;

/// <summary>
/// Creates or upserts the mod's GUI\metadata.lsf.lsx so it declares every converted icon - see
/// META-RECIPE.md for where each rule below comes from (measured from 20,132 real entries).
/// In short: one entry per full-res file, keyed "Assets/&lt;subfolder&gt;/&lt;name&gt;.png", carrying
/// the full-res w/h and mip count; AssetsLowRes files get no entry at all.
/// </summary>
public static class ModMetadata
{
    public const string FileName = "metadata.lsf.lsx";

    public sealed record Entry(string MapKey, int Width, int Height, int MipCount);

    public enum Outcome { Created, Updated, Unchanged, SkippedBinaryOnly, InvalidDocument, Failed }

    public sealed record Result(Outcome Outcome, int Added, int Changed, string Message = "");

    /// <summary>
    /// The mod's metadata file, deduced from the full-res destination. Null when AssetsPath is not an
    /// "...\Assets" folder - guarded on the leaf the tool itself controls, and deliberately never
    /// deduced from AssetsLowResPath (nothing in the metadata refers to the low-res tree).
    /// </summary>
    public static string? DeducePath(string assetsPath)
    {
        if (string.IsNullOrWhiteSpace(assetsPath)) return null;
        try
        {
            var dir = new DirectoryInfo(assetsPath.Trim().TrimEnd('\\', '/'));
            if (!string.Equals(dir.Name, "Assets", StringComparison.OrdinalIgnoreCase)) return null;
            return dir.Parent == null ? null : Path.Combine(dir.Parent.FullName, FileName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>GUI-relative key for one converted icon: always "Assets/", always '/', always ".png" (it names the source image, not the DDS).</summary>
    public static string MapKeyFor(string subfolder, string finalName)
    {
        var folder = (subfolder ?? "").Replace('\\', '/').Trim('/');
        return folder.Length == 0 ? $"Assets/{finalName}.png" : $"Assets/{folder}/{finalName}.png";
    }

    /// <summary>
    /// The binary metadata.lsf that sits beside metadata.lsf.lsx when the BG3 Toolkit manages the mod.
    /// This app can't safely edit that format, so it must not create a competing .lsx next to it.
    /// </summary>
    public static string BinarySiblingPath(string lsxPath)
    {
        var dir = Path.GetDirectoryName(lsxPath) ?? "";
        var name = Path.GetFileName(lsxPath);
        return name.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(dir, name[..^4])
            : Path.Combine(dir, "metadata.lsf");
    }

    const string NewFileSkeleton =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<save>\n" +
        "\t<version major=\"4\" minor=\"8\" revision=\"0\" build=\"500\"/>\n" +
        "\t<region id=\"config\">\n" +
        "\t\t<node id=\"config\">\n" +
        "\t\t\t<children>\n" +
        "\t\t\t\t<node id=\"entries\">\n" +
        "\t\t\t\t\t<children>\n" +
        "\t\t\t\t\t</children>\n" +
        "\t\t\t\t</node>\n" +
        "\t\t\t</children>\n" +
        "\t\t</node>\n" +
        "\t</region>\n" +
        "</save>\n";

    /// <summary>
    /// Creates the file when absent, otherwise adds missing entries and updates changed ones in place.
    /// Never deletes, reorders or reformats entries it didn't touch, matches keys ordinally (FixedString
    /// keys are case-sensitive in the engine), and only writes when something actually changed.
    /// </summary>
    public static Result Upsert(string path, IEnumerable<Entry> entries)
    {
        // Deduplicate on the key itself, not the source - two rows can land on the same subfolder/name.
        var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var entry in entries)
        {
            if (!byKey.ContainsKey(entry.MapKey)) order.Add(entry.MapKey);
            byKey[entry.MapKey] = entry;
        }
        if (order.Count == 0) return new Result(Outcome.Unchanged, 0, 0);

        bool exists = File.Exists(path);
        if (!exists && File.Exists(BinarySiblingPath(path)))
            return new Result(Outcome.SkippedBinaryOnly, 0, 0,
                $"{Path.GetFileName(BinarySiblingPath(path))} (binary, managed by the BG3 Toolkit) already exists there and this app can't edit it safely, so no competing {FileName} was created");

        try
        {
            string original = exists ? File.ReadAllText(path) : NewFileSkeleton;
            var doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace);

            var entriesNode = doc.Root?.Name.LocalName == "save"
                ? doc.Root.Elements("region").FirstOrDefault(e => Id(e) == "config")
                    ?.Elements("node").FirstOrDefault(e => Id(e) == "config")
                    ?.Element("children")
                    ?.Elements("node").FirstOrDefault(e => Id(e) == "entries")
                : null;

            // A file that exists but lacks this structure is a different or corrupted document -
            // report it rather than "repairing" something this tool doesn't understand.
            if (entriesNode == null)
                return new Result(Outcome.InvalidDocument, 0, 0,
                    "the file has no save/region[config]/node[config]/children/node[entries] structure - left untouched");

            var container = entriesNode.Element("children");
            if (container == null)
            {
                container = new XElement("children");
                AppendIndented(entriesNode, container);
            }

            var existing = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var obj in container.Elements("node").Where(e => Id(e) == "Object"))
            {
                var key = AttributeNode(obj, "MapKey")?.Attribute("value")?.Value;
                if (key != null && !existing.ContainsKey(key)) existing[key] = obj;
            }

            int added = 0, changed = 0;
            foreach (var key in order)
            {
                var entry = byKey[key];
                if (existing.TryGetValue(key, out var obj))
                {
                    if (UpdatePayload(obj, entry)) changed++;
                }
                else
                {
                    AppendIndented(container, BuildObject(entry));
                    added++;
                }
            }

            if (added == 0 && changed == 0) return new Result(Outcome.Unchanged, 0, 0);

            File.WriteAllBytes(path, Serialize(doc, original));
            return new Result(exists ? Outcome.Updated : Outcome.Created, added, changed);
        }
        catch (Exception ex)
        {
            return new Result(Outcome.Failed, 0, 0, ex.Message);
        }
    }

    static string? Id(XElement e) => e.Attribute("id")?.Value;

    static XElement? AttributeNode(XElement node, string id) =>
        node.Elements("attribute").FirstOrDefault(a => Id(a) == id);

    static XElement Attr(string id, string type, string value) =>
        new("attribute", new XAttribute("id", id), new XAttribute("type", type), new XAttribute("value", value));

    // Alphabetical, mipcount present - the BG3 Toolkit's own convention.
    static IEnumerable<(string Id, string Type, string Value)> PayloadValues(Entry entry) => new[]
    {
        ("h", "int16", entry.Height.ToString()),
        ("mipcount", "int8", entry.MipCount.ToString()),
        ("w", "int16", entry.Width.ToString()),
    };

    static XElement BuildObject(Entry entry)
    {
        var payload = new XElement("node", new XAttribute("id", "entries"));
        foreach (var (id, type, value) in PayloadValues(entry)) payload.Add(Attr(id, type, value));

        return new XElement("node", new XAttribute("id", "Object"),
            Attr("MapKey", "FixedString", entry.MapKey),
            new XElement("children", payload));
    }

    /// <summary>
    /// Updates only the Object's inner &lt;node id="entries"&gt;, value by value, so anything else a
    /// modder or a future patch put inside the Object (or the payload) survives untouched.
    /// </summary>
    static bool UpdatePayload(XElement obj, Entry entry)
    {
        var children = obj.Element("children");
        if (children == null)
        {
            children = new XElement("children");
            AppendIndented(obj, children);
        }

        var payload = children.Elements("node").FirstOrDefault(e => Id(e) == "entries");
        if (payload == null)
        {
            payload = new XElement("node", new XAttribute("id", "entries"));
            AppendIndented(children, payload);
        }

        bool changed = false;
        foreach (var (id, type, value) in PayloadValues(entry))
        {
            var attr = AttributeNode(payload, id);
            if (attr == null)
            {
                AppendIndented(payload, Attr(id, type, value));
                changed = true;
            }
            else if (attr.Attribute("value")?.Value != value)
            {
                attr.SetAttributeValue("value", value);
                changed = true;
            }
        }
        return changed;
    }

    // ---- Whitespace-preserving insertion --------------------------------------------------
    // The document is loaded with PreserveWhitespace, so indentation lives in XText nodes. New
    // elements get indentation matching their neighbours (tabs or spaces, whichever the file uses),
    // which keeps a modder's diff down to just the lines this tool added.

    static string IndentOf(XElement element)
    {
        if (element.PreviousNode is XText text)
        {
            int newline = text.Value.LastIndexOf('\n');
            if (newline >= 0) return text.Value[(newline + 1)..];
        }
        return "";
    }

    static string IndentUnit(XElement container)
    {
        var parentIndent = IndentOf(container);
        var child = container.Elements().FirstOrDefault();
        if (child != null)
        {
            var childIndent = IndentOf(child);
            if (childIndent.Length > parentIndent.Length && childIndent.StartsWith(parentIndent, StringComparison.Ordinal))
                return childIndent[parentIndent.Length..];
        }
        var root = container.Document?.Root?.Elements().FirstOrDefault();
        var rootIndent = root == null ? "" : IndentOf(root);
        return rootIndent.Length > 0 ? rootIndent : "\t";
    }

    static void AppendIndented(XElement container, XElement child)
    {
        string indent = IndentOf(container);
        string unit = IndentUnit(container);
        string childIndent = indent + unit;
        Indent(child, childIndent, unit);

        if (container.LastNode is XText trailing && string.IsNullOrWhiteSpace(trailing.Value))
            trailing.AddBeforeSelf(new XText("\n" + childIndent), child);
        else
            container.Add(new XText("\n" + childIndent), child, new XText("\n" + indent));
    }

    static void Indent(XElement element, string indent, string unit)
    {
        var kids = element.Elements().ToList();
        if (kids.Count == 0) return;
        foreach (var kid in kids)
        {
            kid.AddBeforeSelf(new XText("\n" + indent + unit));
            Indent(kid, indent + unit, unit);
        }
        element.Add(new XText("\n" + indent));
    }

    /// <summary>
    /// UTF-8 with BOM and an encoding="utf-8" declaration (every sample has both), keeping the
    /// original file's line endings and its "/>" vs " />" style so untouched lines stay byte-identical.
    /// </summary>
    static byte[] Serialize(XDocument doc, string original)
    {
        string newline = original.Contains("\r\n") ? "\r\n" : "\n";
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.Replace,
            NewLineChars = newline,
            OmitXmlDeclaration = doc.Declaration == null,
        };

        string text;
        using (var stream = new MemoryStream())
        {
            using (var writer = XmlWriter.Create(stream, settings))
                doc.Save(writer);
            text = Encoding.UTF8.GetString(stream.ToArray());
        }

        // XmlWriter always writes empty elements as "<x />"; LSLib writes "<x/>".
        if (!original.Contains(" />"))
            text = text.Replace(" />", "/>");

        // XmlWriter drops whatever followed the root element; keep the original's trailing newline.
        if (original.EndsWith("\n") && !text.EndsWith("\n"))
            text += newline;

        var bom = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text);
        var result = new byte[bom.Length + body.Length];
        bom.CopyTo(result, 0);
        body.CopyTo(result, bom.Length);
        return result;
    }
}
