using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Chummer.Presentation.Printing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Collections.Generic;
using System.IO;

namespace Chummer.Tests.Presentation;

[TestClass]
public class WorkspacePrintPdfComposerTests
{
    private const string CoreShapedPrintHtml =
        """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <title>Street Sam Print</title>
          <style>
            body { font-family: 'Segoe UI', sans-serif; margin: 2rem; color: #111827; }
            h1, h2 { margin-bottom: 0.5rem; }
            .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.75rem 1.5rem; }
            .card { border: 1px solid #d1d5db; border-radius: 12px; padding: 1rem 1.25rem; margin-bottom: 1rem; }
            dt { font-weight: 700; }
            dd { margin: 0 0 0.5rem 0; }
          </style>
        </head>
        <body>
          <h1>Street Sam Print</h1>
          <div class="card">
            <h2>Profile</h2>
            <dl class="grid">
              <dt>Alias</dt><dd>Fastpass</dd>
              <dt>Metatype</dt><dd>Human</dd>
              <dt>Build Method</dt><dd>Priority</dd>
              <dt>Player</dt><dd>T&amp;I</dd>
              <dt>Concept</dt><dd></dd>
            </dl>
          </div>
          <div class="card">
            <h2>Progress</h2>
            <dl class="grid">
              <dt>Karma</dt><dd>42.5</dd>
              <dt>Nuyen</dt><dd>12000</dd>
              <dt>Street Cred</dt><dd>3</dd>
              <dt>Initiate Grade</dt><dd>0</dd>
            </dl>
          </div>
          <div class="card">
            <h2>Coverage</h2>
            <dl class="grid">
              <dt>Attributes</dt><dd>8</dd>
              <dt>Skills</dt><dd>12</dd>
              <dt>Gear</dt><dd>5</dd>
              <dt>Contacts</dt><dd>3</dd>
            </dl>
          </div>
        </body>
        </html>
        """;

    [TestMethod]
    public void ParseBlocks_extracts_title_heading_and_card_fields_from_core_print_html()
    {
        var blocks = WorkspacePrintPdfComposer.ParseBlocks(CoreShapedPrintHtml);

        Assert.IsTrue(blocks.Count > 0, "Core-shaped print HTML must yield print blocks.");
        Assert.AreEqual("Street Sam Print", blocks[0].Primary);
        Assert.AreEqual(WorkspacePrintPdfComposer.BlockKind.CardStart, blocks[1].Kind);
        Assert.AreEqual("Profile", blocks[2].Primary);

        Assert.IsTrue(blocks.Any(block =>
            block.Kind == WorkspacePrintPdfComposer.BlockKind.Field
            && string.Equals(block.Primary, "Alias", StringComparison.Ordinal)
            && string.Equals(block.Secondary, "Fastpass", StringComparison.Ordinal)));
        Assert.IsTrue(blocks.Any(block =>
            block.Kind == WorkspacePrintPdfComposer.BlockKind.Field
            && string.Equals(block.Primary, "Player", StringComparison.Ordinal)
            && string.Equals(block.Secondary, "T&I", StringComparison.Ordinal)));
        Assert.IsTrue(blocks.Any(block =>
            block.Kind == WorkspacePrintPdfComposer.BlockKind.Field
            && string.Equals(block.Primary, "Karma", StringComparison.Ordinal)
            && string.Equals(block.Secondary, "42.5", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryCompose_produces_deterministic_valid_pdf_bytes()
    {
        bool first = WorkspacePrintPdfComposer.TryCompose(CoreShapedPrintHtml, out byte[] pdfA, out string? errorA);
        Assert.IsTrue(first, $"composition failed: {errorA}");
        Assert.IsNull(errorA);
        Assert.IsTrue(pdfA.Length > 0);

        bool second = WorkspacePrintPdfComposer.TryCompose(CoreShapedPrintHtml, out byte[] pdfB, out _);
        Assert.IsTrue(second);
        CollectionAssert.AreEqual(pdfA, pdfB, "PDF output must be deterministic for identical input.");

        string ascii = Encoding.ASCII.GetString(pdfA);
        StringAssert.StartsWith(ascii, "%PDF-1.", "PDF bytes must open with a PDF version header.");
        Assert.IsTrue(ascii.EndsWith("%%EOF", StringComparison.Ordinal)
            || ascii.EndsWith("%%EOF\n", StringComparison.Ordinal),
            "PDF bytes must close with the EOF marker.");
        Assert.IsTrue(ascii.Contains("/Type /Catalog", StringComparison.Ordinal), "PDF must contain a catalog object.");
        Assert.IsTrue(ascii.Contains("/Type /Pages", StringComparison.Ordinal), "PDF must contain a page-tree node.");
        // Page objects are asserted via a real parsed page count (see
        // CountPdfPages): a substring check for "/Type /Page" would also match
        // the page-tree node "/Type /Pages" and cannot distinguish them.
        Assert.IsTrue(CountPdfPages(ascii) >= 1, "PDF must contain at least one parsed page object.");
    }

    [TestMethod]
    public void Font_objects_are_pdf_dictionaries_without_binary_string_prefixes()
    {
        Assert.IsTrue(WorkspacePrintPdfComposer.TryCompose(CoreShapedPrintHtml, out byte[] pdf, out string? error), error);
        string ascii = Encoding.ASCII.GetString(pdf);
        int regularFontId = 3 + 2 * CountPdfPages(ascii);
        foreach (int id in new[] { regularFontId, regularFontId + 1 })
        {
            string? body = TryReadObject(ascii, id.ToString(CultureInfo.InvariantCulture));
            Assert.IsNotNull(body);
            StringAssert.StartsWith(body.TrimStart(), "<< /Type /Font");
        }
    }

    [TestMethod]
    public void TryCompose_paginates_long_content()
    {
        StringBuilder longHtml = new();
        longHtml.AppendLine("<!DOCTYPE html>");
        longHtml.AppendLine("<html><head><title>Overflow</title></head><body>");
        longHtml.AppendLine("<h1>Overflow</h1>");
        for (int cardIndex = 0; cardIndex < 40; cardIndex++)
        {
            longHtml.AppendLine($"<div class=\"card\"><h2>Section {cardIndex}</h2><dl>");
            for (int fieldIndex = 0; fieldIndex < 8; fieldIndex++)
            {
                longHtml.AppendLine($"<dt>Field {fieldIndex}</dt><dd>Value line for section {cardIndex}</dd>");
            }

            longHtml.AppendLine("</dl></div>");
        }

        longHtml.AppendLine("</body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(longHtml.ToString(), out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"long composition failed: {error}");

        string ascii = Encoding.ASCII.GetString(pdf);
        int pageCount = CountPdfPages(ascii);
        Assert.IsTrue(pageCount > 1, $"Overflowing print content must paginate across multiple pages; parsed /Count={pageCount}.");
    }

    [TestMethod]
    public void TryCompose_rejects_html_without_a_print_body()
    {
        bool composed = WorkspacePrintPdfComposer.TryCompose("<html><head><title>Empty</title></head><body></body></html>", out byte[] pdf, out string? error);
        Assert.IsFalse(composed, "An empty print body must not produce a PDF.");
        Assert.IsNotNull(error);
        Assert.AreEqual(0, pdf.Length);
    }

    [TestMethod]
    public void Content_stream_serializes_every_Tj_as_parenthesized_string_in_balanced_BT_ET()
    {
        // Item 2 (P1) regression pins: Tj operands must be PDF string literals
        // (parenthesized, WinAnsi-escaped) and every Tj must sit inside a
        // balanced BT/ET text object. The old serializer emitted bare Tj
        // operands (invalid PDF) that no reference extractor could decode.
        bool composed = WorkspacePrintPdfComposer.TryCompose(CoreShapedPrintHtml, out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));

        Assert.IsTrue(content.Contains(" Tj"), "content stream must paint text via Tj");
        Assert.IsTrue(content.Contains("(Alias:)"), "field label text must survive as a real string operand");
        Assert.IsTrue(content.Contains("(  Fastpass)"), "field value text must survive as a real string operand");
        Assert.IsFalse(ContainsBareTjOperand(content),
            "every Tj operand must be a parenthesized string literal");
        Assert.IsTrue(ContainsBalancedTextObjects(content),
            "every Tj must sit inside a balanced BT/ET text object");
    }

    [TestMethod]
    public void Content_stream_positions_label_and_value_runs_without_compounding()
    {
        // A field row paints two runs (bold label, regular value) in one
        // TextOp. The old serializer fed absolute coordinates to the relative
        // Td operator, so the value run drifted to twice its label offset.
        // The fixed serializer opens one BT/ET per run, making each Td
        // absolute: distinct, monotonic, never a 2x compounding.
        bool composed = WorkspacePrintPdfComposer.TryCompose(CoreShapedPrintHtml, out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        List<List<RunPosition>> rows = ExtractRunXGroups(content);

        List<List<RunPosition>> twoRunRows = rows.Where(row => row.Count == 2).ToList();
        Assert.IsTrue(twoRunRows.Count > 0,
            "the print layout must contain at least one two-run field row");
        foreach (List<RunPosition> row in twoRunRows)
        {
            Assert.IsTrue(row[1].X > row[0].X,
                $"the value run must paint right of its label (label {row[0].X}, value {row[1].X})");
            Assert.AreNotEqual(row[0].X * 2f, row[1].X,
                $"run x must not be the compounding signature (label {row[0].X}, value {row[1].X})");
        }
    }

    /// <summary>Inflates all FlateDecode streams in the PDF bytes.</summary>
    private static string ExtractInflatedContentStreams(string ascii)
    {
        var bytes = Encoding.Latin1.GetBytes(ascii);
        var inflated = new StringBuilder();
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            int streamStart = IndexOf(bytes, "stream", cursor);
            if (streamStart < 0)
            {
                break;
            }

            int dataStart = streamStart + "stream".Length;
            if (bytes[dataStart] == (byte)'\r')
            {
                dataStart++;
            }

            if (bytes[dataStart] == (byte)'\n')
            {
                dataStart++;
            }

            int streamEnd = IndexOf(bytes, "endstream", dataStart);
            if (streamEnd < 0)
            {
                break;
            }

            if (!TryInflate(bytes, dataStart, streamEnd - dataStart, inflated))
            {
                // Uncompressed stream (this composer emits raw content
                // streams) - take the bytes verbatim.
                inflated.Append(Encoding.Latin1.GetString(bytes, dataStart, streamEnd - dataStart));
            }

            cursor = streamEnd + "endstream".Length;
        }

        return inflated.ToString();
    }

    private static bool TryInflate(byte[] data, int start, int length, StringBuilder sink)
    {
        try
        {
            using var source = new MemoryStream(data, start, length);
            using var zlib = new ZLibStream(source, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib, Encoding.Latin1);
            sink.Append(reader.ReadToEnd());
            return true;
        }
        catch (InvalidDataException)
        {
            // Not a deflate stream - caller falls back to raw bytes.
            return false;
        }
    }

    private static int IndexOf(byte[] haystack, string needle, int from)
    {
        var needleBytes = Encoding.Latin1.GetBytes(needle);
        int last = haystack.Length - needleBytes.Length;
        for (int i = from; i <= last; i++)
        {
            bool match = true;
            for (int j = 0; j < needleBytes.Length; j++)
            {
                if (haystack[i + j] != needleBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsBareTjOperand(string content)
    {
        // The serializer emits one line per run: "BT /F1 10 Tf x y Td (text) Tj ET".
        // Validity is judged around each Tj token, not by line shape: the
        // operand immediately before Tj must be a balanced parenthesized
        // string literal.
        int cursor = 0;
        while ((cursor = content.IndexOf(" Tj", cursor, StringComparison.Ordinal)) >= 0)
        {
            int end = cursor - 1;
            while (end >= 0 && content[end] == ' ')
            {
                end--;
            }

            if (end < 0 || content[end] != ')')
            {
                return true;
            }

            int depth = 0;
            int open = -1;
            for (int i = end; i >= 0; i--)
            {
                char candidate = content[i];
                if (candidate == ')')
                {
                    depth++;
                }
                else if (candidate == '(')
                {
                    depth--;
                    if (depth == 0)
                    {
                        open = i;
                        break;
                    }
                }
            }

            if (open < 0)
            {
                return true;
            }

            cursor += 3;
        }

        return false;
    }

    private static bool ContainsBalancedTextObjects(string content)
    {
        // Token-level scan: parenthesized string literals are swallowed
        // whole (payloads may contain words like BT/ET/Tj), then BT pushes,
        // ET pops, and every Tj must occur strictly inside a text object.
        int depth = 0;
        foreach (string token in TokenizeOperators(content))
        {
            if (string.Equals(token, "BT", StringComparison.Ordinal))
            {
                depth++;
            }
            else if (string.Equals(token, "ET", StringComparison.Ordinal))
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
            else if (string.Equals(token, "Tj", StringComparison.Ordinal) && depth <= 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static List<string> TokenizeOperators(string content)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < content.Length)
        {
            char current = content[i];
            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            if (current == '(')
            {
                int literalDepth = 1;
                i++;
                while (i < content.Length && literalDepth > 0)
                {
                    if (content[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (content[i] == '(')
                    {
                        literalDepth++;
                    }
                    else if (content[i] == ')')
                    {
                        literalDepth--;
                    }

                    i++;
                }

                continue;
            }

            int start = i;
            while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] != '(')
            {
                i++;
            }

            tokens.Add(content.Substring(start, i - start));
        }

        return tokens;
    }

    private sealed record RunPosition(float X, float Y);

    private static List<List<RunPosition>> ExtractRunXGroups(string content)
    {
        // One BT/ET text object per run; a printed line's runs share an exact
        // baseline y, and distinct lines have distinct y values. Grouping by
        // baseline therefore reconstructs the layout's lines (a field row is
        // a two-run line: bold label + regular value).
        var lines = new Dictionary<float, List<RunPosition>>();
        foreach (string rawLine in content.Split('\n'))
        {
            string trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("BT ", StringComparison.Ordinal) || !trimmed.EndsWith(" Tj ET", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = trimmed.Split(' ');
            int tdIndex = Array.IndexOf(parts, "Td");
            if (tdIndex >= 2
                && float.TryParse(parts[tdIndex - 2], System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[tdIndex - 1], System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                if (!lines.TryGetValue(y, out var bucket))
                {
                    bucket = new List<RunPosition>();
                    lines[y] = bucket;
                }

                bucket.Add(new RunPosition(x, y));
            }
        }

        return lines.Values.ToList();
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    /// <summary>
    /// Parses the real page count from the document structure instead of
    /// counting substrings: "/Type /Page" also matches the page-tree node
    /// "/Type /Pages", so a substring count is not a page-count authority.
    /// Walks trailer -> /Root catalog -> /Pages reference -> /Count and
    /// consumes the xref table to resolve each object. Returns -1 when the
    /// structure cannot be parsed (the caller must fail on -1).
    /// </summary>
    private static int CountPdfPages(string ascii)
    {
        int trailerStart = ascii.IndexOf("trailer", StringComparison.Ordinal);
        if (trailerStart < 0)
        {
            return -1;
        }

        int trailerEnd = ascii.IndexOf("startxref", trailerStart, StringComparison.Ordinal);
        string trailer = trailerEnd > trailerStart
            ? ascii.Substring(trailerStart, trailerEnd - trailerStart)
            : string.Empty;
        Match rootMatch = Regex.Match(trailer, @"/Root\s+(\d+)\s+\d+\s+R");
        if (!rootMatch.Success)
        {
            return -1;
        }

        string? catalogObj = TryReadObject(ascii, rootMatch.Groups[1].Value);
        if (catalogObj is null)
        {
            return -1;
        }

        Match pagesMatch = Regex.Match(catalogObj, @"/Pages\s+(\d+)\s+\d+\s+R");
        if (!pagesMatch.Success)
        {
            return -1;
        }

        string? pagesObj = TryReadObject(ascii, pagesMatch.Groups[1].Value);
        if (pagesObj is null || !pagesObj.Contains("/Type /Pages", StringComparison.Ordinal))
        {
            return -1;
        }

        Match countMatch = Regex.Match(pagesObj, @"/Count\s+(\d+)");
        return countMatch.Success
            ? int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture)
            : -1;
    }

    /// <summary>
    /// Reads the body of the object with the given number by resolving its
    /// offset through the xref table (real xref consumption, so a wrong
    /// table fails the parse rather than substring-walking the file).
    /// </summary>
    private static string? TryReadObject(string ascii, string objectNumber)
    {
        Match xrefOffsetMatch = Regex.Match(ascii, @"startxref\s+(\d+)");
        if (!xrefOffsetMatch.Success
            || !long.TryParse(xrefOffsetMatch.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long xrefOffset))
        {
            return null;
        }

        string xrefMagic = "xref\n";
        if (xrefOffset < 0 || xrefOffset + xrefMagic.Length > ascii.Length
            || ascii.Substring((int)xrefOffset, xrefMagic.Length) != xrefMagic)
        {
            return null;
        }

        int tableStart = (int)xrefOffset + xrefMagic.Length;
        Match sectionMatch = Regex.Match(ascii.Substring(tableStart), @"0\s+(\d+)\s*");
        if (!sectionMatch.Success
            || !int.TryParse(objectNumber, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int wanted))
        {
            return null;
        }

        int entryCount = int.Parse(sectionMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        int entryStart = tableStart + sectionMatch.Index + sectionMatch.Length;
        if (wanted <= 0 || wanted >= entryCount || entryStart + (wanted * 20) + 20 > ascii.Length)
        {
            return null;
        }

        // Each xref entry is exactly 20 bytes: "nnnnnnnnnn ggggg n\r\n".
        Match offsetMatch = Regex.Match(
            ascii.Substring(entryStart + (wanted * 20), 20),
            @"^(\d{10})\s+(\d{5})\s+n");
        if (!offsetMatch.Success)
        {
            return null;
        }

        long objectOffset = long.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        if (objectOffset <= 0 || objectOffset >= ascii.Length)
        {
            return null;
        }

        Match objMatch = Regex.Match(
            ascii.Substring((int)objectOffset),
            @"^\d+\s+\d+\s+obj\b(.*?)endobj",
            RegexOptions.Singleline);
        return objMatch.Success ? objMatch.Groups[1].Value : null;
    }
}
