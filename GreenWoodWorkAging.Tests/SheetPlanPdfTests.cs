using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The printed cutting plan and the window that shows it. The rows below are a real bill, so
    /// the crowded case — a 3-inch strip on an 8 ft sheet — is the one being checked.
    /// </summary>
    public class SheetPlanPdfTests
    {
        private static List<MeasurementEntry> CrowdedBill() => new()
        {
            Row(1, 31,  0, 12,  0, 1),
            Row(2, 17,  6, 14,  0, 1),
            Row(3, 30,  6,  8,  6, 1),
            Row(4, 31,  6, 15,  6, 1),
            Row(5, 28,  0, 15,  6, 1),
            Row(6, 28,  0, 16,  0, 1),
            Row(7, 37,  0, 20,  2, 1),
            Row(8, 96,  0,  3,  0, 10)
        };

        private static MeasurementEntry Row(int serial, int hIn, int hSo, int wIn, int wSo, int qty) => new()
        {
            SerialNumber = serial,
            HeightInches = hIn, HeightSooter = hSo,
            WidthInches  = wIn, WidthSooter  = wSo,
            Quantity     = qty
        };

        private static byte[]? Logo()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Resources", "logo.jpg");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        [Fact]
        public void Generate_WritesAReadablePdf()
        {
            var plan = new SheetCuttingOptimizer().Optimize(CrowdedBill());
            string path = Path.Combine(Path.GetTempPath(), "gwa-plan-test.pdf");

            SheetPlanPdfService.Generate(path, plan, "Muhammad Tariq", new DateTime(2026, 10, 31), Logo());

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 5000, "the plan PDF looks empty");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        /// <summary>Page pictures, so the printed plan can be judged the way the shop will see it.</summary>
        [Fact]
        public void Generate_RendersPagePreviews()
        {
            var plan = new SheetCuttingOptimizer().Optimize(CrowdedBill());
            string dir = Path.Combine(Path.GetTempPath(), "gwa-pdf", DateTime.Now.ToString("HHmmssff"));
            Directory.CreateDirectory(dir);

            SheetPlanPdfService.Build(plan, "Muhammad Tariq", new DateTime(2026, 10, 31), Logo())
                               .GenerateImages(p => Path.Combine(dir, $"plan{p}.png"),
                                   new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 150 });

            Assert.True(File.Exists(Path.Combine(dir, "plan0.png")));
        }

        /// <summary>The Optimize window itself, drawn to a file so its layout can be looked at.</summary>
        [Fact]
        public void ThePlanWindow_DrawsWithACrowdedBill()
        {
            var plan = new SheetCuttingOptimizer().Optimize(CrowdedBill());
            string dir = Path.Combine(Path.GetTempPath(), "gwa-sheets");
            Directory.CreateDirectory(dir);

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new SheetOptimizerForm(plan, "Muhammad Tariq");
                    form.Size = new System.Drawing.Size(1120, 640);

                    // A form that has never been shown paints nothing but its own background.
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(Path.Combine(dir, "plan-window.png"), System.Drawing.Imaging.ImageFormat.Png);

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Drawing the plan window never finished.");

            Assert.Null(failure);
        }
    }
}
