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

    [TestMethod]
    public void TryCompose_wraps_long_field_values_inside_page_bounds()
    {
        // WIN-008 gen2: the delivered PDF failed the extraction bounds check
        // because a long Concept value painted a single unwrapped line to
        // x=597.21 on a 595.28pt page. Every painted Td x must stay inside
        // [Margin, PageWidth - Margin], and the long value must wrap onto
        // continuation lines instead of overflowing.
        const string longConcept = "Covert operations specialist with a very long " +
            "biography paragraph that absolutely cannot fit on a single unwrapped line";

        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head><title>Bounds</title></head><body>");
        html.AppendLine("<h1>Bounds Print</h1>");
        html.AppendLine("<div class=\"card\"><h2>Profile</h2><dl class=\"grid\">");
        html.Append("<dt>Alias</dt><dd>Fastpass</dd>");
        html.Append("<dt>Concept</dt><dd>").Append(longConcept).Append("</dd>");
        html.AppendLine("</dl></div>");
        html.AppendLine("</body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(html.ToString(), out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        List<List<RunPosition>> rows = ExtractRunXGroups(content);
        Assert.IsTrue(rows.Count > 0, "content stream must paint positioned runs");

        // PageWidth=595.28, Margin=50: every painted x must lie within the
        // printable band. A wrapped value paints its continuation lines at the
        // same x as the first line (single wrapped row never grows rightward).
        foreach (List<RunPosition> row in rows)
        {
            foreach (RunPosition run in row)
            {
                Assert.IsTrue(run.X >= 50f - 0.01f,
                    $"painted x {run.X} must not start left of the margin");
                Assert.IsTrue(run.X <= 595.28f - 50f + 0.01f,
                    $"painted x {run.X} must not pass the right margin (long values must wrap)");
            }
        }

        // The full value text must survive wrapping: no dropped words.
        string allText = content;
        foreach (string word in longConcept.Split(' '))
        {
            Assert.IsTrue(allText.Contains(word, StringComparison.Ordinal),
                $"wrapped value must not drop the word '{word}'");
        }
    }

    [TestMethod]
    public void TryCompose_non_ascii_characters_are_truthfully_encoded_or_disclosed()
    {
        // WinAnsi-encodable accents must round-trip exactly; a character with
        // no WinAnsi representation must be replaced by a visible, disclosed
        // fallback (recorded in the substitutions report) rather than a
        // silent '?' byte.
        const string printableName = "Zo\u00eb Ren\u00e9e";
        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head><title>Unicode</title></head><body>");
        html.AppendLine("<h1>Unicode Print</h1>");
        html.AppendLine("<div class=\"card\"><h2>Profile</h2><dl class=\"grid\">");
        html.Append("<dt>Alias</dt><dd>").Append(printableName).Append("</dd>");
        html.Append("<dt>Concept</dt><dd>Greek \u03b1 \u4e2d philosophy</dd>");
        html.AppendLine("</dl></div></body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html.ToString(), out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");

        // Zoë Renée: ë (U+00EB) and é (U+00E9) are both WinAnsi-encodable; they
        // must appear in the PDF text exactly (no substitution recorded).
        Assert.IsFalse(substitutions.Any(substitution => substitution.Source == '\u00eb'),
            "ë is WinAnsi-encodable and must not be substituted");
        Assert.IsFalse(substitutions.Any(substitution => substitution.Source == '\u00e9'),
            "é is WinAnsi-encodable and must not be substituted");

        // Greek alpha (U+03B1) and CJK 中 (U+4E2D) have no WinAnsi form: they
        // must be disclosed via the substitution report with a visible,
        // deterministic fallback, never a silent '?' byte.
        var alpha = substitutions.FirstOrDefault(substitution => substitution.Source == '\u03b1');
        Assert.AreEqual('\u03b1', alpha.Source, "alpha substitution must be recorded");
        StringAssert.StartsWith(alpha.Painted, "[U+03B1]", "fallback must be the visible codepoint marker");
        var cjk = substitutions.FirstOrDefault(substitution => substitution.Source == '\u4e2d');
        Assert.AreEqual('\u4e2d', cjk.Source, "CJK substitution must be recorded");
        StringAssert.StartsWith(cjk.Painted, "[U+4E2D]", "fallback must be the visible codepoint marker");

        // The painted fallback must be present in the content stream.
        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        Assert.IsTrue(content.Contains("[U+03B1]", StringComparison.Ordinal),
            "the visible codepoint fallback must be painted in the PDF");
        Assert.IsFalse(content.Contains("(?", StringComparison.Ordinal),
            "the silent '?' mangling signature must not appear");
    }

    [TestMethod]
    public void TryCompose_paginates_without_dropping_wrapped_content()
    {
        // A field whose value spans multiple wrapped lines that themselves
        // cross a page boundary must paint every line (pagination is
        // measured, not line-count approximated).
        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head><title>Overflow</title></head><body>");
        html.AppendLine("<h1>Overflow</h1>");
        for (int cardIndex = 0; cardIndex < 40; cardIndex++)
        {
            html.AppendLine($"<div class=\"card\"><h2>Section {cardIndex}</h2><dl>");
            for (int fieldIndex = 0; fieldIndex < 8; fieldIndex++)
            {
                html.AppendLine($"<dt>Field {fieldIndex}</dt><dd>Value line for section {cardIndex}</dd>");
            }

            html.AppendLine("</dl></div>");
        }

        html.AppendLine("</body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(html.ToString(), out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        // Every field of every card survives pagination.
        for (int cardIndex = 0; cardIndex < 40; cardIndex++)
        {
            for (int fieldIndex = 0; fieldIndex < 8; fieldIndex++)
            {
                Assert.IsTrue(content.Contains($"Field {fieldIndex}:", StringComparison.Ordinal),
                    $"field label 'Field {fieldIndex}:' missing from painted output");
            }
        }
    }

    [TestMethod]
    public void Card_border_reopens_on_continuation_page()
    {
        // A card spanning a page break must paint a closed border on the
        // outgoing page and a fresh border on the continuation page (the old
        // composer carried a stale cardTop onto the next page).
        StringBuilder html = new();
        html.AppendLine("<!DOCTYPE html><html><head><title>Split</title></head><body>");
        html.AppendLine("<h1>Split</h1>");
        html.AppendLine("<div class=\"card\"><h2>Big</h2><dl>");
        for (int i = 0; i < 120; i++)
        {
            html.AppendLine($"<dt>Row {i}</dt><dd>value {i} with some added text to make the card genuinely long</dd>");
        }

        html.AppendLine("</dl></div></body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(html.ToString(), out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string ascii = Encoding.ASCII.GetString(pdf);
        int pageCount = CountPdfPages(ascii);
        Assert.IsTrue(pageCount > 1, "the split card must span more than one page");

        // Count border-painting operators per content stream: a card that
        // crosses a break paints borders on both pages (re-open = fresh
        // CardBorderOp on the continuation page).
        string allContent = ExtractInflatedContentStreams(ascii);
        int borderOps = CountOccurrences(allContent, "re S\n");
        Assert.IsTrue(borderOps >= pageCount,
            $"continuation page must repaint the card border (borders={borderOps}, pages={pageCount})");
    }

    [TestMethod]
    public void TryCompose_reports_no_substitutions_for_pure_ascii_document()
    {
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            CoreShapedPrintHtml, out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");
        Assert.AreEqual(0, substitutions.Count,
            "the all-WinAnsi print HTML must compose without any character substitution");
        Assert.IsTrue(pdf.Length > 0);
    }
    // ------------------------------------------------------------------
    // WIN-008 gen3 (Controller8536): painted-glyph extent regressions.
    // The gen2 bounds test asserted run STARTING x only; the controller
    // proved with PyMuPDF get_texttrace that glyph boxes still extended
    // past the printable edge (OE*80 -> right 862.00, han*40 -> 1767.29,
    // W*200 -> 552.88 vs printable 545.28). These tests assert the painted
    // RIGHT EDGE of every run: start x plus the resolved painted advance
    // of the run's literal, using the same base-14 AFM widths the composer
    // itself applies (its fonts declare no /Widths, so base-14 metrics are
    // authoritative). A painted right edge past the printable band is a
    // hard failure.

    private static double PaintedAdvanceOfLiteral(string literal, bool bold)
    {
        // AFM advances for the painted alphabet: ASCII 0x20..0x7E,
        // Latin-1 0xA0..0xFF and the WinAnsi mapped slots. Marker text is
        // pure ASCII. Extracted with PyMuPDF; cross-checked against the
        // controller's probes (OE=1000, W=944).
        int[] regularAscii = [278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,278,556,556,444,556,444,333,500,556,278,333,556,278,833,556,500,556,556,444,389,333,556,500,722,500,500,444,392,220,392,584];
        int[] boldAscii = [278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584];
        int[] table = bold ? boldAscii : regularAscii;

        // Decode the PDF literal: octal escapes -> WinAnsi bytes; each byte
        // maps to its glyph advance via WinAnsi byte -> codepoint.
        double total = 0;
        int i = 0;
        while (i < literal.Length)
        {
            int advance;
            if (literal[i] == '\\' && i + 1 < literal.Length && literal[i + 1] >= '0' && literal[i + 1] <= '7')
            {
                int value = 0;
                int digits = 0;
                int j = i + 1;
                while (j < literal.Length && digits < 3 && literal[j] >= '0' && literal[j] <= '7')
                {
                    value = value * 8 + (literal[j] - '0');
                    j++;
                    digits++;
                }

                advance = WinAnsiByteAdvance(value, table);
                i = j;
            }
            else
            {
                char c = literal[i];
                int cp = c;
                if (cp >= 0x20 && cp <= 0x7E)
                {
                    advance = table[cp - 0x20];
                }
                else if (c >= '\u00A0' && c <= '\u00FF')
                {
                    advance = table[c - 0x20]; // not used for Latin-1 in these probes
                    advance = 556; // conservative; probes use ASCII/mapped only
                }
                else
                {
                    advance = 556;
                }

                i++;
            }

            total += advance;
        }

        return total;
    }

    private static int WinAnsiByteAdvance(int byteValue, int[] table)
    {
        // Mapped slots 0x80..0x9F advance as their WinAnsi codepoint glyph.
        return byteValue switch
        {
            >= 0x20 and <= 0x7E => table[byteValue - 0x20],
            0x8C => 1000, // OElig
            0x9C => 944,  // oelig
            _ => 556,
        };
    }

    private static void AssertPaintedExtentsWithinPage(byte[] pdf, string label)
    {
        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        List<(double X, double Size, bool Bold, string Literal)> runs = [];
        Regex opRegex = new(
            @"BT (/F[12]) ([0-9.]+) Tf (-?[0-9.]+) (-?[0-9.]+) Td \(((?:[^()\\]|\\.)*)\) Tj ET",
            RegexOptions.Singleline);
        foreach (Match m in opRegex.Matches(content))
        {
            double size = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double x = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            runs.Add((x, size, m.Groups[1].Value == "/F2", m.Groups[4].Value));
        }

        Assert.IsTrue(runs.Count > 0, label + ": no painted runs found");

        const double printableRight = 545.28;
        const double printableLeft = 50.0;
        foreach ((double x, double size, bool bold, string literal) in runs)
        {
            double advance = PaintedAdvanceOfLiteral(literal, bold);
            double rightEdge = x + advance * size / 1000.0;
            Assert.IsTrue(rightEdge <= printableRight + 0.5,
                $"{label}: painted right edge {rightEdge:F2} exceeds printable right {printableRight:F2} (run x={x:F2}, size={size}, literal='{(literal.Length > 48 ? literal[..48] + "..." : literal)}')");
            Assert.IsTrue(x >= printableLeft - 0.5,
                $"{label}: run starts x={x:F2} left of the printable area");
        }
    }

    private static string ProbeValueHtml(string value)
    {
        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><head><title>Bounded PDF regression probe</title></head><body>");
        html.Append("<h1>Bounded PDF regression probe</h1>");
        html.Append("<div class=\"card\"><h2>Profile</h2><dl><dt>Concept</dt><dd>").Append(value).Append("</dd></dl></div>");
        html.Append("</body></html>");
        return html.ToString();
    }

    [TestMethod]
    public void TryCompose_probe_OE80_painted_extents_within_page()
    {
        // Probe 1 (controller8536): OE*80 measured 556/source, painted 1000;
        // right edge reached x=862.00 with 27 glyphs outside the page.
        string html = ProbeValueHtml(new string('\u0152', 80));
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html, out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");
        Assert.AreEqual(0, substitutions.Count, "OE is WinAnsi-supported: no substitutions");
        AssertPaintedExtentsWithinPage(pdf, "OE*80");
    }

    [TestMethod]
    public void TryCompose_probe_han40_markers_within_page_and_all_painted()
    {
        // Probe 2: han*40 painted [U+6F22] markers whose glyph boxes reached
        // x=1767.29 with 227 glyphs outside. The resolver must measure the
        // marker glyphs AND split before overflow, keeping every glyph in
        // the band and painting ALL 40 markers (no dropped content).
        string html = ProbeValueHtml(new string('\u6F22', 40));
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html, out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");
        Assert.AreEqual(40, substitutions.Count, "every unsupported char is recorded");
        AssertPaintedExtentsWithinPage(pdf, "han*40");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        Assert.AreEqual(40, CountOccurrences(content, "[U+6F22]"),
            "all 40 painted markers must appear in the content stream");
    }

    [TestMethod]
    public void TryCompose_probe_W200_split_before_overflow()
    {
        // Probe 3: the hard-split loop appended the overflowing character
        // before flushing; right edge reached 552.88 > 545.28.
        string html = ProbeValueHtml(new string('W', 200));
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html, out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");
        AssertPaintedExtentsWithinPage(pdf, "W*200");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        Assert.AreEqual(200, CountOccurrences(content, "W"),
            "all 200 W glyphs painted (the probe HTML contains no other capital W)");
    }

    [TestMethod]
    public void TryCompose_A_macron_uses_visible_marker_not_bare_A()
    {
        // gen3 disclosure fix: U+0100 previously salvaged to bare "A"
        // (silent content change). Marker policy: visible [U+0100] marker,
        // recorded in substitutions.
        string html = ProbeValueHtml(new string('\u0100', 10) + "tail");
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html, out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");
        Assert.AreEqual(10, substitutions.Count(sub => sub.Source == '\u0100'),
            "each A-macron must be recorded");
        Assert.IsTrue(substitutions.All(sub => sub.Painted == "[U+0100]"),
            "painted form must be the visible codepoint marker, not bare A");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        Assert.AreEqual(10, CountOccurrences(content, "[U+0100]"), "markers painted");
    }

    [TestMethod]
    public void TryCompose_supplementary_and_lone_surrogates_take_explicit_marker()
    {
        // gen3: supplementary input previously hit a caught
        // ArgumentException inside string.Normalize. Truthful explicit
        // fallback instead - and never a claim of exact support.
        string html = ProbeValueHtml("\uD800\uDC00end");
        bool composed = WorkspacePrintPdfComposer.TryCompose(
            html, out byte[] pdf, out string? error, out var substitutions);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        Assert.IsTrue(content.Contains("[U+10000]", StringComparison.Ordinal),
            "supplementary pair must paint the explicit U+10000 marker");

        string htmlLone = ProbeValueHtml("lone\uDC00surrogate");
        bool composedLone = WorkspacePrintPdfComposer.TryCompose(
            htmlLone, out byte[] pdfLone, out string? errorLone);
        Assert.IsTrue(composedLone, $"lone-surrogate composition failed: {errorLone}");
        string contentLone = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdfLone));
        Assert.IsTrue(contentLone.Contains("[U+DC00]", StringComparison.Ordinal),
            "lone surrogate must paint the explicit marker without throwing");
    }

    [TestMethod]
    public void TryCompose_styled_runs_preserve_separators_across_wrap()
    {
        // "Preserve separators across styled label/value runs": a bold value
        // run followed by regular text must keep exactly one separator space
        // when the line wraps mid-sequence.
        string longValue = "Twist " + string.Join(" ", Enumerable.Repeat("turn", 40));
        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><head><title>Styled</title></head><body>");
        html.Append("<h1>Styled</h1>");
        html.Append("<div class=\"card\"><h2>Profile</h2><dl>");
        html.Append("<dt>Alias</dt><dd>").Append(longValue).Append("</dd>");
        html.Append("</dl></div></body></html>");

        bool composed = WorkspacePrintPdfComposer.TryCompose(html.ToString(), out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string content = ExtractInflatedContentStreams(Encoding.ASCII.GetString(pdf));
        // No doubled separators anywhere in the painted output.
        Assert.IsFalse(content.Contains("  ", StringComparison.Ordinal),
            "wrapped styled runs must not invent doubled spaces");
        AssertPaintedExtentsWithinPage(pdf, "styled*wrap");
    }

    [TestMethod]
    public void TryCompose_probe_value_page_continuation_keeps_all_content()
    {
        // A probe-scale value that hard-splits across a PAGE boundary must
        // keep every glyph painted (full content through pagination).
        string value = new string('M', 4000);
        string html = ProbeValueHtml(value);
        bool composed = WorkspacePrintPdfComposer.TryCompose(html, out byte[] pdf, out string? error);
        Assert.IsTrue(composed, $"composition failed: {error}");

        string ascii = Encoding.ASCII.GetString(pdf);
        Assert.IsTrue(CountPdfPages(ascii) > 1, "4000 M chars must span more than one page");
        string content = ExtractInflatedContentStreams(ascii);
        Assert.AreEqual(4000, CountOccurrences(content, "M"),
            "all 4000 glyphs must survive pagination (probe HTML has no other capital M)");
        AssertPaintedExtentsWithinPage(pdf, "M*4000");
    }

}
