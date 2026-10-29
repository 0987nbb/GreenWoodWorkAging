using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The customer bill as an A4 PDF: shop header, measurement table, tape / gari /
    /// sheet-cut charges and the final amount.
    /// </summary>
    public class BillPdfServiceTests
    {
        private static readonly List<MeasurementEntry> FourRows = new()
        {
            new() { SerialNumber = 1, HeightInches = 240, HeightSooter = 2, WidthInches = 24, Quantity = 2, Side = MeasurementSide.Both },
            new() { SerialNumber = 2, HeightInches = 120, HeightSooter = 6, WidthInches = 18, WidthSooter = 2, Quantity = 1 },
            new() { SerialNumber = 3, HeightInches = 96,  WidthInches = 12, Quantity = 1, Side = MeasurementSide.HeightHeightWidth },
            new() { SerialNumber = 4, HeightInches = 48,  HeightSooter = 4, WidthInches = 2, WidthSooter = 5, Quantity = 1, Side = MeasurementSide.WidthWidthHeight }
        };

        private static BillPdfData Sample(BillCharges? charges = null, IReadOnlyList<MeasurementEntry>? rows = null)
        {
            string logoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Resources", "logo.jpg");
            return new BillPdfData
            {
                CustomerName = "Muhammad Tariq",
                Phone        = "0300 1234567",
                Date         = new DateTime(2026, 10, 29),
                BillNumber   = 42,
                Rows         = rows ?? FourRows,
                Charges      = charges ?? new BillCharges { RatePerFoot = 30, CarriageFare = 1000, SheetCuts = 40, RatePerSheet = 200 },
                Logo         = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null
            };
        }

        [Fact]
        public void Generate_WritesAReadablePdf()
        {
            string path = Path.Combine(Path.GetTempPath(), "gwa-bill-test.pdf");
            BillPdfService.Generate(path, Sample());

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 2000, "the PDF looks empty");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public void Generate_WithoutCharges_StillProducesABill()
        {
            string path = Path.Combine(Path.GetTempPath(), "gwa-bill-nocharges-test.pdf");
            BillPdfService.Generate(path, Sample(new BillCharges()));

            Assert.True(new FileInfo(path).Length > 2000);
        }

        /// <summary>A bill longer than one page must keep flowing, not blow up the layout.</summary>
        [Fact]
        public void Generate_LongBill_Paginates()
        {
            var many = Enumerable.Range(1, 45).Select(i => new MeasurementEntry
            {
                SerialNumber = i,
                HeightInches = 60 + i,
                HeightSooter = i % 8,
                WidthInches  = 24,
                WidthSooter  = (i * 3) % 8,
                Quantity     = 1 + i % 4
            }).ToList();

            string path = Path.Combine(Path.GetTempPath(), "gwa-bill-long-test.pdf");
            BillPdfService.Generate(path, Sample(rows: many));

            Assert.True(new FileInfo(path).Length > 4000);
        }

        /// <summary>Rasterised pages, kept in %TEMP%\gwa-pdf for a visual check of the layout.</summary>
        [Fact]
        public void Build_RendersPagePreviews()
        {
            // A fresh folder per run: an old preview left on disk looks exactly like a new one.
            var dir = Path.Combine(Path.GetTempPath(), "gwa-pdf", DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);

            BillPdfService.Build(Sample()).GenerateImages(page => Path.Combine(dir, $"short{page}.png"),
                new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 });

            BillPdfService.Build(Sample(rows: Enumerable.Range(1, 45)
                .Select(i => new MeasurementEntry { SerialNumber = i, HeightInches = 60 + i, WidthInches = 24, Quantity = 2 })
                .ToList())).GenerateImages(page => Path.Combine(dir, $"long{page}.png"),
                new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 });

            Assert.True(File.Exists(Path.Combine(dir, "short0.png")));
        }
    }
}
