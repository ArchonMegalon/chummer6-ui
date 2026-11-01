using System.Globalization;
using System.Net;
using System.Text;

namespace Chummer.Presentation.Printing;

/// <summary>
/// Renders the workspace print HTML emitted by Core's <c>WorkspaceService.BuildPrintHtml</c>
/// into a real PDF 1.4 document. Pure managed BCL: base-14 Helvetica fonts with
/// WinAnsiEncoding, A4 pages, deterministic bytes (no timestamps, no compression
/// variance), so identical print receipts compose identical PDFs.
/// </summary>
public static class WorkspacePrintPdfComposer
{
    private const float PageWidth = 595.28f;  // A4 portrait, points
    private const float PageHeight = 841.89f;
    private const float Margin = 50f;
    private const float CardPadding = 12f;
    private const float CardBorderGray = 0.72f;

    public const string PdfFileExtension = ".pdf";
    public const string HtmlFileExtension = ".html";

    /// <summary>Attempts to compose a PDF from the Core print HTML. Never throws for
    /// content problems: returns false with an error reason instead.</summary>
    public static bool TryCompose(string printHtml, out byte[] pdf, out string? error)
    {
        pdf = [];
        error = null;

        if (string.IsNullOrWhiteSpace(printHtml))
        {
            error = "print-html-empty";
            return false;
        }

        try
        {
            IReadOnlyList<PrintBlock> blocks = ParseBlocks(printHtml);
            if (blocks.Count == 0)
            {
                error = "print-html-no-recognized-blocks";
                return false;
            }

            pdf = Compose(blocks);
            return pdf.Length > 0;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            error = "print-pdf-compose-failed:" + ex.GetType().Name;
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // HTML parsing (Core's BuildPrintHtml inventory: h1, div.card > h2, dl > dt/dd)
    // ---------------------------------------------------------------------

    internal enum BlockKind
    {
        DocumentTitle,
        CardStart,
        CardEnd,
        SectionHeading,
        Field
    }

    internal sealed record PrintBlock(BlockKind Kind, string Primary, string? Secondary = null);

    internal static IReadOnlyList<PrintBlock> ParseBlocks(string html)
    {
        List<PrintBlock> blocks = [];
        bool insideCard = false;

        foreach ((string tag, string attributes, string inner) in ScanTags(html))
        {
            switch (tag)
            {
                case "h1":
                    blocks.Add(new PrintBlock(BlockKind.DocumentTitle, Decode(inner)));
                    break;

                case "div" when attributes.Contains("card", StringComparison.Ordinal):
                    insideCard = true;
                    blocks.Add(new PrintBlock(BlockKind.CardStart, string.Empty));
                    break;

                case "h2":
                    blocks.Add(new PrintBlock(BlockKind.SectionHeading, Decode(inner)));
                    break;

                case "dt":
                    blocks.Add(new PrintBlock(BlockKind.Field, Decode(inner)));
                    break;

                case "dd":
                    if (blocks.Count > 0
                        && blocks[^1].Kind == BlockKind.Field
                        && blocks[^1].Secondary is null)
                    {
                        blocks[^1] = blocks[^1] with { Secondary = Decode(inner) };
                    }

                    break;

                case "/div" when insideCard:
                    insideCard = false;
                    blocks.Add(new PrintBlock(BlockKind.CardEnd, string.Empty));
                    break;

                case "/body":
                    if (insideCard)
                    {
                        blocks.Add(new PrintBlock(BlockKind.CardEnd, string.Empty));
                    }

                    break;
            }
        }

        return blocks.Where(static block
                => block.Kind != BlockKind.Field
                || !string.IsNullOrEmpty(block.Primary))
            .ToArray();
    }

    private static IEnumerable<(string Tag, string Attributes, string Inner)> ScanTags(string html)
    {
        // Predefined tags we track; content runs until the matching close tag
        // (Core's emitted HTML never nests these).
        string[] tracked = ["h1", "h2", "dt", "dd", "title"];

        int position = 0;
        while (position < html.Length)
        {
            int open = html.IndexOf('<', position);
            if (open < 0)
            {
                yield break;
            }

            int close = html.IndexOf('>', open);
            if (close < 0)
            {
                yield break;
            }

            string tagToken = html[(open + 1)..close].Trim();
            bool isClose = tagToken.StartsWith("/", StringComparison.Ordinal);
            if (isClose)
            {
                tagToken = tagToken[1..].Trim();
            }

            int space = tagToken.IndexOfAny([' ', '\t', '\r', '\n']);
            string tagName = (space >= 0 ? tagToken[..space] : tagToken).ToLowerInvariant();
            string attributes = space >= 0 ? tagToken[(space + 1)..] : string.Empty;

            if (!isClose && (tagName is "div" or "body"))
            {
                // Container tags: consume the opening tag; the close is yielded
                // separately when encountered.
                position = close + 1;
                yield return (tagName, attributes, string.Empty);
                continue;
            }

            if (!isClose && Array.IndexOf(tracked, tagName) >= 0)
            {
                string closeTag = "</" + tagName;
                int innerEnd = html.IndexOf(closeTag, close + 1, StringComparison.OrdinalIgnoreCase);
                if (innerEnd < 0)
                {
                    yield break;
                }

                string inner = html[(close + 1)..innerEnd];
                int tagEnd = html.IndexOf('>', innerEnd);
                if (tagEnd < 0)
                {
                    yield break;
                }

                position = tagEnd + 1;
                yield return (tagName, attributes, inner);
                continue;
            }

            if (isClose && (tagName is "div" or "body"))
            {
                position = close + 1;
                yield return ("/" + tagName, string.Empty, string.Empty);
                continue;
            }

            position = close + 1;
        }
    }

    private static string Decode(string value)
    {
        string collapsed = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return WebUtility.HtmlDecode(collapsed);
    }

    // ---------------------------------------------------------------------
    // Layout + PDF writing
    // ---------------------------------------------------------------------

    private enum TextRunStyle
    {
        Regular,
        Bold
    }

    private sealed record TextRun(string Text, TextRunStyle Style, float Size);

    private static byte[] Compose(IReadOnlyList<PrintBlock> blocks)
    {
        List<PageOp> ops = [];
        float y = PageHeight - Margin;

        void EnsureRoom(float needed)
        {
            if (y - needed < Margin)
            {
                ops.Add(new PageOp(PageBreakOp.Instance));
                y = PageHeight - Margin;
            }
        }

        bool cardOpen = false;
        float cardTop = 0f;

        void DrawRuns(IReadOnlyList<TextRun> runs, float indent)
        {
            float lineHeight = runs.Count > 0 ? runs.Max(static run => run.Size) * 1.45f : 12f;
            EnsureRoom(lineHeight);
            float x = Margin + indent;
            y -= lineHeight * 0.82f;
            float baseline = y;
            ops.Add(new PageOp(new TextOp(runs, x, baseline)));
            y -= lineHeight * 0.18f;
        }

        foreach (PrintBlock block in blocks)
        {
            switch (block.Kind)
            {
                case BlockKind.DocumentTitle:
                    DrawRuns([new TextRun(block.Primary, TextRunStyle.Bold, 20f)], 0f);
                    y -= 6f;
                    break;

                case BlockKind.CardStart:
                    EnsureRoom(48f);
                    cardOpen = true;
                    cardTop = y;
                    break;

                case BlockKind.CardEnd:
                    if (cardOpen)
                    {
                        float cardBottom = y - 4f;
                        ops.Add(new PageOp(new CardBorderOp(cardTop, cardBottom)));
                        y = cardBottom - 12f;
                        cardOpen = false;
                    }

                    break;

                case BlockKind.SectionHeading:
                    y -= 4f;
                    DrawRuns([new TextRun(block.Primary, TextRunStyle.Bold, 14f)], CardPadding);
                    y -= 2f;
                    break;

                case BlockKind.Field:
                    string key = block.Primary;
                    string value = block.Secondary ?? string.Empty;
                    DrawRuns(
                    [
                        new TextRun(key + ":", TextRunStyle.Bold, 10f),
                        new TextRun("  " + value, TextRunStyle.Regular, 10f)
                    ], CardPadding);
                    break;
            }
        }

        if (cardOpen)
        {
            ops.Add(new PageOp(new CardBorderOp(cardTop, y - 4f)));
        }

        return WritePdf(ops);
    }

    private abstract record PageContentOp;

    private sealed record TextOp(IReadOnlyList<TextRun> Runs, float X, float Baseline) : PageContentOp;

    private sealed record CardBorderOp(float Top, float Bottom) : PageContentOp;

    private sealed record PageOp(PageContentOp Content);

    private sealed record PageBreakOp : PageContentOp
    {
        public static readonly PageBreakOp Instance = new();
        private PageBreakOp()
        {
        }
    }

    // ---------------------------------------------------------------------
    // PDF serializer
    // ---------------------------------------------------------------------

    private static byte[] WritePdf(IReadOnlyList<PageOp> ops)
    {
        // Split into pages.
        List<List<PageContentOp>> pages = [new List<PageContentOp>()];
        foreach (PageOp op in ops)
        {
            if (op.Content is PageBreakOp)
            {
                pages.Add(new List<PageContentOp>());
            }
            else
            {
                pages[^1].Add(op.Content);
            }
        }

        // Fixed object numbering: 1 catalog, 2 pages tree, then per page
        // (page, contents) interleaved, then the two fonts, then info.
        int pageCount = pages.Count;
        int fontRegularId = 3 + (2 * pageCount);
        int fontBoldId = fontRegularId + 1;
        int infoId = fontBoldId + 1;

        MemoryStream buffer = new();
        using BinaryWriter raw = new(buffer, Encoding.ASCII, leaveOpen: true);

        List<long> offsets = [];
        long Position() => raw.BaseStream.Position;

        raw.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"u8.ToArray());

        void BeginObject(int id)
        {
            offsets.Add(Position());
            raw.Write(Encoding.ASCII.GetBytes($"{id} 0 obj\n"));
        }

        // Catalog + pages tree.
        BeginObject(1);
        raw.Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"u8.ToArray());
        BeginObject(2);
        StringBuilder kids = new();
        kids.Append('[');
        for (int i = 0; i < pageCount; i++)
        {
            if (i > 0)
            {
                kids.Append(' ');
            }

            kids.Append(CultureInfo.InvariantCulture, $"{3 + (2 * i)} 0 R");
        }

        kids.Append(']');
        raw.Write(Encoding.ASCII.GetBytes(
            $"<< /Type /Pages /Kids {kids} /Count {pageCount} >>\nendobj\n"));

        // Page objects + content streams.
        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            int pageId = 3 + (2 * pageIndex);
            int contentId = pageId + 1;

            BeginObject(pageId);
            raw.Write(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Format(PageWidth)} {Format(PageHeight)}] " +
                $"/Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >> /Contents {contentId} 0 R >>\nendobj\n"));

            byte[] stream = RenderContentStream(pages[pageIndex]);
            BeginObject(contentId);
            raw.Write(Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n"));
            raw.Write(stream);
            raw.Write("\nendstream\nendobj\n"u8.ToArray());
        }

        BeginObject(fontRegularId);
        raw.Write(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n"u8.ToArray());
        BeginObject(fontBoldId);
        raw.Write(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(infoId);
        raw.Write("<< /Producer (Chummer6 WorkspacePrintPdfComposer) >>\nendobj\n"u8.ToArray());

        // xref + trailer.
        long xrefOffset = Position();
        StringBuilder xref = new();
        xref.Append(CultureInfo.InvariantCulture, $"xref\n0 {offsets.Count + 1}\n");
        xref.Append("0000000000 65535 f \n");
        foreach (long offset in offsets)
        {
            xref.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        raw.Write(Encoding.ASCII.GetBytes(xref.ToString()));
        raw.Write(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info {infoId} 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));

        return buffer.ToArray();
    }

    private static byte[] RenderContentStream(IReadOnlyList<PageContentOp> contents)
    {
        StringBuilder content = new();
        foreach (PageContentOp op in contents)
        {
            switch (op)
            {
                case TextOp text:
                    AppendTextOp(content, text);
                    break;

                case CardBorderOp border:
                    content.Append(CultureInfo.InvariantCulture,
                        $"{CardBorderGray.ToString("0.0##", CultureInfo.InvariantCulture)} G " +
                        $"0.8 w {Format(Margin)} {Format(border.Bottom)} " +
                        $"{Format(PageWidth - (2 * Margin))} {Format(border.Top - border.Bottom)} re S\n");
                    break;
            }
        }

        return Encoding.Latin1.GetBytes(content.ToString());
    }

    private static void AppendTextOp(StringBuilder content, TextOp text)
    {
        content.Append("BT\n");
        float x = text.X;
        foreach (TextRun run in text.Runs)
        {
            string font = run.Style == TextRunStyle.Bold ? "/F2" : "/F1";
            content.Append(CultureInfo.InvariantCulture,
                $"{font} {Format(run.Size)} Tf {Format(x)} {Format(text.Baseline)} Td ");
            content.Append(EscapeWinAnsi(run.Text));
            content.Append(" Tj\n");
            x += MeasureWidth(run.Text, run.Style, run.Size);
        }

        content.Append("ET\n");
    }

    private static string Format(float value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string EscapeWinAnsi(string text)
    {
        Encoding winAnsi = Encoding.Latin1;
        StringBuilder escaped = new(text.Length + 8);
        foreach (char character in text)
        {
            switch (character)
            {
                case '(':
                case ')':
                case '\\':
                    escaped.Append('\\').Append(character);
                    break;

                default:
                    if (WinAnsiMap.TryGetValue(character, out string? mapped))
                    {
                        escaped.Append(mapped);
                        continue;
                    }

                    byte encoded = winAnsi.GetBytes([character])[0];
                    if (character <= 0xFF && encoded == character)
                    {
                        escaped.Append(character);
                    }
                    else
                    {
                        escaped.Append(CultureInfo.InvariantCulture,
                            $"\\{Convert.ToString(encoded, 8).PadLeft(3, '0')}");
                    }

                    break;
            }
        }

        return escaped.ToString();
    }

    /// <summary>Unicode code points outside Latin-1 that WinAnsiEncoding places in
    /// the 0x80–0x9F range. Everything else Latin-1 passes through byte-identically.</summary>
    private static readonly IReadOnlyDictionary<char, string> WinAnsiMap =
        new Dictionary<char, string>
        {
            ['\u20AC'] = "\\200", // €
            ['\u201A'] = "\\202", // ‚
            ['\u0192'] = "\\203", // ƒ
            ['\u201E'] = "\\204", // „
            ['\u2026'] = "\\205", // …
            ['\u2020'] = "\\206", // †
            ['\u2021'] = "\\207", // ‡
            ['\u02C6'] = "\\210", // ˆ
            ['\u2030'] = "\\211", // ‰
            ['\u0160'] = "\\212", // Š
            ['\u2039'] = "\\213", // ‹
            ['\u0152'] = "\\214", // Œ
            ['\u017D'] = "\\216", // Ž
            ['\u2018'] = "\\221", // '
            ['\u2019'] = "\\222", // '
            ['\u201C'] = "\\223", // "
            ['\u201D'] = "\\224", // "
            ['\u2022'] = "\\225", // •
            ['\u2013'] = "\\226", // –
            ['\u2014'] = "\\227", // —
            ['\u02DC'] = "\\230", // ˜
            ['\u2122'] = "\\231", // ™
            ['\u0161'] = "\\232", // š
            ['\u203A'] = "\\233", // ›
            ['\u0153'] = "\\234", // œ
            ['\u017E'] = "\\236", // ž
            ['\u0178'] = "\\237"  // Ÿ
        };

    private static float MeasureWidth(string text, TextRunStyle style, float size)
    {
        IReadOnlyList<short> widths = style == TextRunStyle.Bold ? BoldWidths : RegularWidths;
        float total = 0f;
        foreach (char character in text)
        {
            int index = character - 32;
            if (index >= 0 && index < widths.Count)
            {
                total += widths[index];
            }
            else
            {
                total += 556; // conservative default for WinAnsi extras
            }
        }

        return total * size / 1000f;
    }

    // Adobe core14 AFM widths for chars 32..126 (' '~'~').
    private static readonly IReadOnlyList<short> RegularWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    private static readonly IReadOnlyList<short> BoldWidths =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584
    ];
}
