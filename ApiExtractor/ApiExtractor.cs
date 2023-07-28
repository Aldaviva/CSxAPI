using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiExtractor.Extraction;
using ApiExtractor.Generation;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace ApiExtractor;

internal class ApiExtractor {

    private const string DOCUMENTATION_DIRECTORY = @"..\..\..\Documentation\";

    public static async Task Main() {
        string pdfFilename      = Path.Combine(DOCUMENTATION_DIRECTORY, "api-reference-guide.pdf");
        string eventXmlFilename = Path.Combine(DOCUMENTATION_DIRECTORY, "event.xml");

        // dumpWordsOnPage(args); return;

        Stopwatch              stopwatch = Stopwatch.StartNew();
        ExtractedDocumentation docs      = new();
        PdfReader.parsePdf(pdfFilename, docs);
        new EventReader(docs).parseEventXml(eventXmlFilename);
        new Fixes(docs).fix();
        await CsClientWriter.writeClient(docs);

        stopwatch.Stop();
        Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:N3} seconds.");
    }

    private static void dumpWordsOnPage() {
        using PdfDocument pdf = PdfDocument.Open(Path.Combine(DOCUMENTATION_DIRECTORY, "api-reference-guide.pdf"));

        Page page           = pdf.GetPage(59);
        bool leftSideOfPage = false;

        IWordExtractor wordExtractor = DefaultWordExtractor.Instance;
        IReadOnlyList<Letter> lettersWithUnfuckedQuotationMarks = page.Letters
            .Where(letter => PdfReader.isTextOnHalfOfPage(letter, page, leftSideOfPage))
            /*.Select(letter => letter switch {
                { Value: "\"", PointSize: 9.6, FontName: var fontName } when fontName.EndsWith("CourierNewPSMT") => new Letter(
                    letter.Value,
                    letter.GlyphRectangle,
                    new PdfPoint(letter.StartBaseLine.X, Math.Round(letter.StartBaseLine.Y, 4)),
                    new PdfPoint(letter.EndBaseLine.X, Math.Round(letter.EndBaseLine.Y, 4)),
                    letter.Width,
                    letter.FontSize,
                    letter.Font,
                    letter.Color,
                    8.8,
                    letter.TextSequence),
                _ => letter
            })*/.ToImmutableList();
        IEnumerable<Word> pageText = wordExtractor.GetWords(lettersWithUnfuckedQuotationMarks);

        // IComparer<Word> wordPositionComparer = new WordPositionComparer();
        foreach (Word textBlock in pageText) {
            Letter firstLetter = textBlock.Letters[0];
            // Console.WriteLine(textBlock.Text);
            Console.WriteLine($@"{textBlock.Text}
    character style = {PdfReader.getCharacterStyle(textBlock)}
    typeface = {firstLetter.Font.Name.Split('+', 2).Last()}
    point size = {firstLetter.PointSize:N3}
    italic = {firstLetter.Font.IsItalic}
    bold = {firstLetter.Font.IsBold}
    weight = {firstLetter.Font.Weight:N}
    position = ({firstLetter.Location.X:N}, {firstLetter.Location.Y:N})
    baseline = {firstLetter.StartBaseLine.Y:N3}
    bounds bottom = {textBlock.BoundingBox.Bottom:N}
    height (bounds) = {textBlock.BoundingBox.Height:N}
    height (transformed) = {firstLetter.PointSize:N}
    capline = {firstLetter.StartBaseLine.Y + textBlock.BoundingBox.Height:N}
    topline = {firstLetter.StartBaseLine.Y + firstLetter.PointSize:N}
    color = {firstLetter.Color}
    text sequence = {firstLetter.TextSequence:N0}
");
        }
    }

}