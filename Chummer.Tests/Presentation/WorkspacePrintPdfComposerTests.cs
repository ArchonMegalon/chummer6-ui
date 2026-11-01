using System;
using System.Linq;
using System.Text;
using Chummer.Presentation.Printing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        Assert.IsTrue(ascii.Contains("/Type /Page", StringComparison.Ordinal), "PDF must contain page objects.");
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
        int pageCount = CountOccurrences(ascii, "/Type /Page");
        Assert.IsTrue(pageCount > 1, "Overflowing print content must paginate across multiple pages.");
    }

    [TestMethod]
    public void TryCompose_rejects_html_without_a_print_body()
    {
        bool composed = WorkspacePrintPdfComposer.TryCompose("<html><head><title>Empty</title></head><body></body></html>", out byte[] pdf, out string? error);
        Assert.IsFalse(composed, "An empty print body must not produce a PDF.");
        Assert.IsNotNull(error);
        Assert.AreEqual(0, pdf.Length);
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
}
