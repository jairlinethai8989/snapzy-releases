using System.IO.Compression;
using System.Xml.Linq;
using System.Net;
using System.Text;

namespace SnapCraft;

internal sealed record ReportImage(string Title, string Caption, byte[] Png);
internal static class ReportDocument
{
    private static readonly XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    internal static string Html(string title, string summary, string template, IReadOnlyList<ReportImage> images, string? templateLabel = null)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var sections = string.Join("", images.Select((image, index) => $"<article><h2>{index + 1}. {E(image.Title)}</h2><p>{E(image.Caption)}</p><img src='{index}.png'></article>"));
        return $"<!doctype html><html><head><meta charset='utf-8'><style>body{{font:14px 'Segoe UI',sans-serif;color:#17212b;max-width:760px;margin:24px auto}}h1{{font-size:24px}}h2{{font-size:17px}}p{{white-space:pre-wrap}}img{{max-width:100%;max-height:225mm;object-fit:contain}}article{{break-inside:avoid;margin:20px 0}}@page{{size:A4;margin:14mm}}@media print{{article{{break-before:page}}}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:16px}}.pair article{{break-before:auto}}</style></head><body><h1>{E(title)}</h1><p>{E(templateLabel ?? template)}</p><p>{E(summary)}</p><main class='{(template == "Before / after" ? "pair" : "")}'>{sections}</main></body></html>";
    }
    internal static byte[] Word(string title, string summary, IReadOnlyList<ReportImage> images)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteXml(zip, "[Content_Types].xml", new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")),
                new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", "/word/document.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))));
            WriteXml(zip, "_rels/.rels", new XElement(rel + "Relationships", Relationship("rId1", "officeDocument", "word/document.xml")));
            var body = new XElement(w + "body", Paragraph(title, true), Paragraph(summary));
            var links = new XElement(rel + "Relationships");
            for (var index = 0; index < images.Count; index++)
            {
                var image = images[index]; var id = $"rId{index + 1}";
                links.Add(Relationship(id, "image", $"media/{index}.png"));
                using (var file = zip.CreateEntry($"word/media/{index}.png", CompressionLevel.NoCompression).Open()) file.Write(image.Png);
                using var source = new MemoryStream(image.Png); using var bitmap = new Bitmap(source);
                var ratio = Math.Min(6.3 * 914400 / bitmap.Width, 8.7 * 914400 / bitmap.Height);
                body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "br", new XAttribute(w + "type", "page")))));
                body.Add(Paragraph($"{index + 1}. {image.Title}", true), Paragraph(image.Caption), ImageParagraph(id, index + 1, (long)(bitmap.Width * ratio), (long)(bitmap.Height * ratio)));
            }
            body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", "11906"), new XAttribute(w + "h", "16838")), new XElement(w + "pgMar", new XAttribute(w + "top", "794"), new XAttribute(w + "bottom", "794"), new XAttribute(w + "left", "794"), new XAttribute(w + "right", "794"))));
            WriteXml(zip, "word/document.xml", new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), new XAttribute(XNamespace.Xmlns + "r", r), body));
            WriteXml(zip, "word/_rels/document.xml.rels", links);
        }
        return output.ToArray();
    }
    private static XElement Relationship(string id, string type, string target) => new(rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/" + type), new XAttribute("Target", target));
    private static XElement Paragraph(string text, bool bold = false)
    {
        var run = new XElement(w + "r", bold ? new XElement(w + "rPr", new XElement(w + "b"), new XElement(w + "sz", new XAttribute(w + "val", "32"))) : null);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n')) { if (run.Elements(w + "t").Any()) run.Add(new XElement(w + "br")); run.Add(new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), line)); }
        return new XElement(w + "p", run);
    }
    private static void WriteXml(ZipArchive zip, string name, XElement root) { using var stream = zip.CreateEntry(name).Open(); new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream); }
    private static XElement ImageParagraph(string id, int index, long width, long height)
    {
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        var drawing = new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", width), new XAttribute("cy", height)), new XElement(wp + "docPr", new XAttribute("id", index), new XAttribute("name", $"Image {index}")),
            new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic",
                new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", index), new XAttribute("name", $"Image {index}")), new XElement(pic + "cNvPicPr")),
                new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", id)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", width), new XAttribute("cy", height))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))))));
        return new XElement(w + "p", new XElement(w + "r", new XElement(w + "drawing", drawing)));
    }
}
