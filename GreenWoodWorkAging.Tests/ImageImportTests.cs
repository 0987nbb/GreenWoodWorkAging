using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// The text half of image import: what a read size list turns into. These tests only cover
    /// the parser and the reader — no calculation, bill or optimizer behaviour is involved.
    /// </summary>
    public class SizeListParserTests
    {
        private static ImportedSize SingleSize(string text)
        {
            var parsed = SizeListParser.Parse(text);
            Assert.Single(parsed.Sizes);
            return parsed.Sizes[0];
        }

        [Theory]
        [InlineData("89 x 24")]
        [InlineData("89*24")]
        [InlineData("89 24")]
        [InlineData("89X24")]
        [InlineData("Height 89 Width 24")]
        public void TwoNumbers_AreHeightAndWidthWithQuantityOne(string text)
        {
            var s = SingleSize(text);

            Assert.Equal(89, s.HeightInches);
            Assert.Equal(0, s.HeightSooter);
            Assert.Equal(24, s.WidthInches);
            Assert.Equal(1, s.Quantity);
        }

        [Theory]
        [InlineData("89 x 24 x 2")]
        [InlineData("89*24*2")]
        [InlineData("89 24 2")]
        [InlineData("89 x 24 qty 2")]
        public void ThreeNumbers_ThirdOneIsTheQuantity(string text)
        {
            var s = SingleSize(text);

            Assert.Equal(89 * 8, s.HeightEighths);
            Assert.Equal(24 * 8, s.WidthEighths);
            Assert.Equal(2, s.Quantity);
        }

        [Fact]
        public void OneDigitAfterThePoint_IsSooter_LikeTheManualBox()
        {
            var s = SingleSize("79.7 x 24.3");

            Assert.Equal(79, s.HeightInches);
            Assert.Equal(7, s.HeightSooter);
            Assert.Equal(24, s.WidthInches);
            Assert.Equal(3, s.WidthSooter);
        }

        [Fact]
        public void TwoDigitsAfterThePoint_DecimalInches_RoundedToEighths()
        {
            var s = SingleSize("79.75 x 24");

            Assert.Equal(79, s.HeightInches);
            Assert.Equal(6, s.HeightSooter);   // .75 in = 6/8
        }

        [Fact]
        public void AFraction_BelongsToTheWholeNumberInFrontOfIt()
        {
            var s = SingleSize("240 2/8 x 24");

            Assert.Equal(240, s.HeightInches);
            Assert.Equal(2, s.HeightSooter);
            Assert.Equal(24, s.WidthInches);
            Assert.Equal(1, s.Quantity);       // the fraction must not steal the quantity slot
        }

        [Fact]
        public void QuarterAndHalfInches_AlsoMerge()
        {
            Assert.Equal(4, SingleSize("12 1/2 x 6").HeightSooter);   // 4/8
            Assert.Equal(2, SingleSize("12 1/4 x 6").HeightSooter);   // 2/8
        }

        [Theory]
        [InlineData("1. 89 x 24 x 2")]
        [InlineData("2) 89 x 24 x 2")]
        [InlineData("12] 89 x 24 x 2")]
        public void AListIndexInTheFront_IsIgnored(string text)
        {
            var s = SingleSize(text);

            Assert.Equal(89, s.HeightInches);
            Assert.Equal(2, s.Quantity);
        }

        [Fact]
        public void TwoSizesOnOneLine_SplitBySemicolon()
        {
            var parsed = SizeListParser.Parse("89x24; 80x18x3");

            Assert.Equal(2, parsed.Sizes.Count);
            Assert.Equal(89, parsed.Sizes[0].HeightInches);
            Assert.Equal(80, parsed.Sizes[1].HeightInches);
            Assert.Equal(3, parsed.Sizes[1].Quantity);
        }

        [Fact]
        public void AWholeList_IsReadLineByLine()
        {
            var parsed = SizeListParser.Parse(
                "SIZES\r\n" +
                "1. 89 x 24 x 2\r\n" +
                "2. 80 x 18\r\n" +
                "3. 79.7 x 24.3\r\n");

            Assert.Equal(3, parsed.Sizes.Count);
            Assert.Empty(parsed.UnreadLines);
            Assert.Equal(79 * 8 + 7, parsed.Sizes[2].HeightEighths);
            Assert.Equal(24 * 8 + 3, parsed.Sizes[2].WidthEighths);
        }

        [Theory]
        [InlineData("89 x 24 x 2 x 3")]     // too many numbers to know which is which
        [InlineData("890")]                  // a lone number
        [InlineData("890 x 24")]             // longer than any board the shop cuts
        [InlineData("89 x 24 x 2.5")]        // a quantity that is not a count of pieces
        [InlineData("0 x 24")]               // no height
        public void LinesThatCannotBeTrusted_AreReportedNotGuessed(string text)
        {
            var parsed = SizeListParser.Parse(text);

            Assert.Empty(parsed.Sizes);
            Assert.Single(parsed.UnreadLines);
            Assert.Equal(text, parsed.UnreadLines[0]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Google Image Result")]
        [InlineData("-----")]
        public void NoiseWithoutNumbers_IsSkippedSilently(string text)
        {
            var parsed = SizeListParser.Parse(text);

            Assert.Empty(parsed.Sizes);
            Assert.Empty(parsed.UnreadLines);
        }

        [Fact]
        public void SourceLineIsKeptSoTheReadingCanBeChecked()
        {
            var s = SingleSize("89 x 24 x 2");

            Assert.Equal("89 x 24 x 2", s.SourceLine);
        }

        [Fact]
        public void ImportedSizes_FlowIntoTheSheetPlanLikeTypedOnes()
        {
            var parsed = SizeListParser.Parse("89 x 24 x 2\n80 x 18\n60 x 6");

            var rows = new List<MeasurementEntry>();
            int serial = 1;
            foreach (var s in parsed.Sizes)
            {
                rows.Add(new MeasurementEntry
                {
                    SerialNumber = serial++,
                    HeightInches = s.HeightInches, HeightSooter = s.HeightSooter,
                    WidthInches  = s.WidthInches,  WidthSooter  = s.WidthSooter,
                    Quantity     = s.Quantity,
                    Side         = MeasurementSide.Both
                });
            }

            var plan = new SheetCuttingOptimizer().Optimize(rows);

            Assert.Equal(4, plan.PieceCount);        // the first row is quantity 2
            Assert.Empty(plan.Unplaced);
            Assert.Equal(4, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.InRange(plan.Sheets.Count, 1, 4);
        }
    }

    /// <summary>
    /// The picture half: a rendered size list goes in, sizes come out. This is the same code
    /// path the Import button uses, minus the file picker and the grid.
    /// </summary>
    public class ImageSizeImporterTests : IDisposable
    {
        private readonly string _directory;

        public ImageSizeImporterTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "gwa-import", DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            // Keep the images so a failure can be looked at by eye.
        }

        private string WriteImage(string name, string[] lines, float fontSize = 34f)
        {
            int width  = 760;
            int height = 60 + lines.Length * 64;
            var path = Path.Combine(_directory, name);

            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using var font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            for (int i = 0; i < lines.Length; i++)
                g.DrawString(lines[i], font, Brushes.Black, new PointF(36, 32 + i * 64));

            bmp.Save(path, ImageFormat.Png);
            return path;
        }

        [Fact]
        public void APictureOfASizeList_GivesUpItsSizes()
        {
            string path = WriteImage("list.png", new[] { "89 x 24 x 2", "80 x 18", "79.7 x 24.3" });

            using var importer = new ImageSizeImporter();
            var result = importer.ImportOne(path);

            File.WriteAllText(Path.Combine(_directory, "ocr.txt"), result.RawText);
            Assert.Null(result.Error);
            Assert.Equal(3, result.Sizes.Count);

            Assert.Equal(89 * 8,      result.Sizes[0].HeightEighths);
            Assert.Equal(24 * 8,      result.Sizes[0].WidthEighths);
            Assert.Equal(2,           result.Sizes[0].Quantity);
            Assert.Equal(80 * 8,      result.Sizes[1].HeightEighths);
            Assert.Equal(79 * 8 + 7,  result.Sizes[2].HeightEighths);
            Assert.Equal(24 * 8 + 3,  result.Sizes[2].WidthEighths);
        }

        [Fact]
        public void SeveralPicturesAtOnce_AreAllRead()
        {
            string first  = WriteImage("a.png", new[] { "89 x 24" });
            string second = WriteImage("b.png", new[] { "60 x 6 x 3" });

            using var importer = new ImageSizeImporter();
            var results = importer.Import(new[] { first, second });

            Assert.Equal(2, results.Count);
            Assert.Equal(89, results[0].Sizes.Single().HeightInches);
            Assert.Equal(3,  results[1].Sizes.Single().Quantity);
            Assert.All(results, r => Assert.Null(r.Error));
        }

        [Fact]
        public void AFileThatIsNotAPicture_IsReportedPerFile()
        {
            string note = Path.Combine(_directory, "notes.txt");
            File.WriteAllText(note, "89 x 24");
            string good = WriteImage("c.png", new[] { "80 x 18" });

            using var importer = new ImageSizeImporter();
            var results = importer.Import(new[] { note, good });

            Assert.NotNull(results[0].Error);
            Assert.Empty(results[0].Sizes);
            Assert.Single(results[1].Sizes);   // the readable picture still gets through
        }

        [Fact]
        public void AMissingFile_IsReportedNotThrown()
        {
            using var importer = new ImageSizeImporter();
            var result = importer.ImportOne(Path.Combine(_directory, "does-not-exist.png"));

            Assert.NotNull(result.Error);
            Assert.Empty(result.Sizes);
        }

        [Fact]
        public void WithoutLanguageData_TheErrorSaysWhatIsMissing()
        {
            string empty = Path.Combine(_directory, "no-tessdata");
            Directory.CreateDirectory(empty);

            using var importer = new ImageSizeImporter(empty);
            var result = importer.ImportOne(WriteImage("d.png", new[] { "89 x 24" }));

            Assert.NotNull(result.Error);
            Assert.Contains("eng.traineddata", result.Error);
        }

        /// <summary>
        /// A shop keeper does not scan, he photographs: ruled paper, blue pen, the camera tilted
        /// a little and his own hand throwing a shadow across the page. This is that picture.
        /// </summary>
        internal static string WritePhoto(string directory, string name, string[] lines, float tiltDegrees)
        {
            int width = 900, height = 60 + lines.Length * 90;
            var path = Path.Combine(directory, name);

            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(246, 244, 238));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            g.TranslateTransform(width / 2f, height / 2f);
            g.RotateTransform(tiltDegrees);
            g.TranslateTransform(-width / 2f, -height / 2f);

            using (var rule = new Pen(Color.FromArgb(190, 205, 220), 2f))
                for (int i = 0; i < lines.Length; i++)
                {
                    float y = 66 + i * 90;
                    g.DrawLine(rule, 20, y, width - 20, y);
                }

            using var ink = new SolidBrush(Color.FromArgb(35, 55, 145));
            using var font = new Font("Segoe UI", 40f, FontStyle.Regular, GraphicsUnit.Pixel);
            for (int i = 0; i < lines.Length; i++)
                g.DrawString(lines[i], font, ink, new PointF(40, 24 + i * 90));

            g.ResetTransform();

            // The photographer's shadow: one half of the page noticeably darker than the other.
            using var shadow = new LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.FromArgb(0, 0, 0, 0), Color.FromArgb(110, 0, 0, 0),
                LinearGradientMode.Horizontal);
            g.FillRectangle(shadow, 0, 0, width, height);

            bmp.Save(path, ImageFormat.Jpeg);
            return path;
        }

        [Fact]
        public void APhotographOfASizeList_IsStillRead()
        {
            string path = WritePhoto(_directory, "photo.jpg",
                new[] { "89 x 24 x 2", "80 x 18", "60 x 30 x 3" }, tiltDegrees: 3f);

            // Save what the cleaner produced so the pipeline can be judged by eye.
            using (var cleaned = ImagePreprocessor.Prepare(path))
                cleaned.Save(Path.Combine(_directory, "photo-cleaned.png"), ImageFormat.Png);

            using var importer = new ImageSizeImporter();
            var result = importer.ImportOne(path);

            File.WriteAllText(Path.Combine(_directory, "ocr-photo.txt"), result.RawText);
            Assert.Null(result.Error);
            Assert.Equal(3, result.Sizes.Count);

            Assert.Equal(89 * 8, result.Sizes[0].HeightEighths);
            Assert.Equal(24 * 8, result.Sizes[0].WidthEighths);
            Assert.Equal(2,      result.Sizes[0].Quantity);
            Assert.Equal(80 * 8, result.Sizes[1].HeightEighths);
            Assert.Equal(18 * 8, result.Sizes[1].WidthEighths);
            Assert.Equal(1,      result.Sizes[1].Quantity);
            Assert.Equal(60 * 8, result.Sizes[2].HeightEighths);
            Assert.Equal(3,      result.Sizes[2].Quantity);
        }

        [Fact]
        public void TheCleaner_HandsBackAPictureInTheSameShape()
        {
            string path = WritePhoto(_directory, "shape.jpg", new[] { "89 x 24" }, tiltDegrees: -4f);

            using var raw = new Bitmap(path);
            using var cleaned = ImagePreprocessor.Prepare(path);

            Assert.Equal(raw.Width,  cleaned.Width);
            Assert.Equal(raw.Height, cleaned.Height);
            cleaned.Save(Path.Combine(_directory, "shape-cleaned.png"), ImageFormat.Png);
        }
    }
}
