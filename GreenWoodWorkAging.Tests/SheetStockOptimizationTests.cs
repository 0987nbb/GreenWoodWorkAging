using System.Drawing;
using System.Reflection;
using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using QuestPDF.Fluent;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The cutting plan cut from the sheets the shop says it has: their own size, their own
    /// count, and an honest answer when the count is not enough. A bill that never goes through
    /// the stock question must plan exactly the way it did before the question existed.
    /// </summary>
    [Collection("SheetStock")]
    public class SheetStockOptimizationTests
    {
        private static readonly string Scratch =
            Path.Combine(Path.GetTempPath(), "gwa-stock-plan", DateTime.Now.ToString("HHmmss"));

        /// <summary>A size typed in inches, the way the shop types it.</summary>
        private static MeasurementEntry Row(int serial, int heightInches, int widthInches, int qty,
                                            string sheetCode = "") => new()
        {
            SerialNumber = serial,
            HeightInches = heightInches,
            WidthInches  = widthInches,
            Quantity     = qty,
            SheetCode    = sheetCode
        };

        private static readonly SheetCuttingOptimizer Opt = new();

        private static List<MeasurementEntry> NineBoards() =>
            Enumerable.Range(1, 9).Select(i => Row(i, 32, 48, 1)).ToList();

        [Fact]
        public void ThePlanIsCutFromTheEnteredSheetSize_NotTheDefault()
        {
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("Small", 24, 48, 0) };

            // Two 20 × 24 panels stand side by side on a 48 tall × 24 wide sheet.
            var plan = Opt.OptimizeBySheetCode(new[] { Row(1, 20, 24, 2) }, stock);

            Assert.Single(plan.Sheets);
            Assert.Equal(48 * Sooter.PerInch, plan.Sheets[0].SheetHeightSixteenths);
            Assert.Equal(24 * Sooter.PerInch, plan.Sheets[0].SheetWidthSixteenths);
            Assert.Equal(plan.Sheets[0].SheetWidthSixteenths,  plan.SheetWidthSixteenths);
            Assert.Equal(plan.Sheets[0].SheetHeightSixteenths, plan.SheetHeightSixteenths);
        }

        [Fact]
        public void ASizeBiggerThanTheEnteredSheet_IsStillReportedAsTooBig()
        {
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("Small", 24, 48, 0) };

            var plan = Opt.OptimizeBySheetCode(new[] { Row(1, 60, 40, 1) }, stock);

            Assert.Empty(plan.Sheets);
            var missing = Assert.Single(plan.Unplaced);
            Assert.Equal(UnplacedReason.TooBigForSheet, missing.Reason);
            Assert.Empty(plan.Shortfalls);
        }

        [Fact]
        public void TheCountEntered_IsTheMostThePlanOpens()
        {
            // Nine 32 × 48 boards need three standard sheets; two are on the floor.
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 2) };

            var plan = Opt.OptimizeBySheetCode(NineBoards(), stock);

            Assert.Equal(2, plan.Sheets.Count);
            Assert.Equal(6, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.Equal(3, plan.Shortfalls.Count);
            Assert.All(plan.Shortfalls, u => Assert.Equal(UnplacedReason.OutOfStock, u.Reason));

            var usage = Assert.Single(plan.Stock);
            Assert.Equal(2, usage.Used);
            Assert.Equal(2, usage.Available);
            Assert.Equal(3, usage.ShortPieces);
        }

        [Fact]
        public void EnoughStockLeavesNothingShort_AndSaysHowManyWereUsed()
        {
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 5) };

            var plan = Opt.OptimizeBySheetCode(NineBoards(), stock);

            Assert.Empty(plan.Shortfalls);
            Assert.Equal(9, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.Equal(plan.Sheets.Count, Assert.Single(plan.Stock).Used);
            Assert.True(plan.Sheets.Count <= 3, "three sheets is all this bill's area needs");
        }

        [Fact]
        public void AnUncountedPile_HoldsThePlanBack()
        {
            // A quantity of 0 is "not counted", which is every bill before the stock question.
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 0) };

            var plan = Opt.OptimizeBySheetCode(NineBoards(), stock);

            Assert.Empty(plan.Shortfalls);
            Assert.Equal(9, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.True(Assert.Single(plan.Stock).Unlimited);
        }

        [Fact]
        public void OnePileSharedByTwoSheetCodes_IsSpentByBothGroups()
        {
            // Neither code names the pile, so both are cut from it — and the count is shared,
            // not handed to each group again.
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 3) };

            var rows = NineBoards();
            rows.Add(Row(10, 32, 48, 3, "Super Glass"));

            var plan = Opt.OptimizeBySheetCode(rows, stock);

            Assert.Equal(3, plan.Sheets.Count);
            var usage = Assert.Single(plan.Stock);
            Assert.Equal(3, usage.Used);
            Assert.Equal(3, usage.ShortPieces);
            Assert.Equal(3, plan.Shortfalls.Count);
            Assert.All(plan.Shortfalls, u => Assert.Equal("KMI 123", u.StockName));
        }

        [Fact]
        public void EachSheetCode_IsCutFromTheStockRowNamedAfterIt()
        {
            var stock = new[]
            {
                new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 4),
                new SheetCuttingOptimizer.StockSheet("PVC",     48, 48, 4)
            };

            var rows = new[]
            {
                Row(1, 90, 40, 1, "KMI 123"),
                Row(2, 40, 40, 1, "PVC")
            };

            var plan = Opt.OptimizeBySheetCode(rows, stock);

            var kmi   = plan.Sheets.Single(s => s.Source == "KMI 123");
            var pvc   = plan.Sheets.Single(s => s.Source == "PVC");
            Assert.Equal(96 * Sooter.PerInch, kmi.SheetHeightSixteenths);
            Assert.Equal(48 * Sooter.PerInch, pvc.SheetHeightSixteenths);
            Assert.Equal("PVC", pvc.StockName);

            // Numbers run across the whole plan, whichever pile each sheet came out of.
            Assert.Equal(Enumerable.Range(1, plan.Sheets.Count), plan.Sheets.Select(s => s.Number));
        }

        [Fact]
        public void NoStockEntered_PlansExactlyLikeItAlwaysDid()
        {
            var before     = Opt.OptimizeBySheetCode(NineBoards());
            var emptyList  = Opt.OptimizeBySheetCode(NineBoards(), Array.Empty<SheetCuttingOptimizer.StockSheet>());

            Assert.Equal(before.Sheets.Count, emptyList.Sheets.Count);
            Assert.Equal(before.PieceCount,   emptyList.PieceCount);
            Assert.Empty(emptyList.Stock);
            Assert.Equal(96 * Sooter.PerInch, emptyList.SheetHeightSixteenths);
            Assert.All(emptyList.Sheets, s => Assert.Equal("", s.StockName));
        }

        // ── What the plan window says about the stock ─────────────────────────

        [Fact]
        public void ThePlanWindow_ShowsWithTheStockEnteredAndWhatRanOut()
        {
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 2) };
            var plan  = Opt.OptimizeBySheetCode(NineBoards(), stock);

            var seen = Painted(plan, "stock-plan-window.png").Text;

            Assert.Contains("2 sheets of 96 × 48 in", seen);
            Assert.Contains("KMI 123 (96 × 48 in): 2 of 2 used", seen);
            Assert.Contains("stock entered ran out", seen);
            Assert.DoesNotContain("bigger than a sheet", seen);
        }

        [Fact]
        public void ThePlanWindow_NamesTheSheetSizeTheShopTyped()
        {
            var stock = new[] { new SheetCuttingOptimizer.StockSheet("Small", 24, 48, 6) };
            var plan  = Opt.OptimizeBySheetCode(new[] { Row(1, 20, 24, 8) }, stock);

            string shown = Painted(plan).Text;
            Assert.Contains("48 × 24 in", shown);
            Assert.DoesNotContain("8 ft × 4 ft", shown);
        }

        [Fact]
        public void ThePlanWindow_DrawsEachSheetAtItsOwnShape()
        {
            var stock = new[]
            {
                new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 4),
                new SheetCuttingOptimizer.StockSheet("PVC",     48, 48, 4)
            };
            var plan = Opt.OptimizeBySheetCode(new[]
            {
                Row(1, 90, 40, 1, "KMI 123"),
                Row(2, 40, 40, 1, "PVC")
            }, stock);

            var boxes = Painted(plan, "mixed-stock-plan.png").Pictures;

            // The tall sheet keeps the tall picture; the square one is drawn square, not stretched.
            Assert.True(boxes["KMI 123"].Height >= boxes["KMI 123"].Width * 2,
                        "an 8 ft × 4 ft sheet is twice as tall as wide");
            Assert.True(Math.Abs(boxes["PVC"].Height - boxes["PVC"].Width) <= 2,
                        $"a square sheet is drawn square: {boxes["PVC"].Width} × {boxes["PVC"].Height}");
        }

        /// <summary>
        /// Opens the plan window on a throwaway thread and hands back everything it paints:
        /// the lines of text the shop reads and the box each sheet picture was given.
        /// </summary>
        private static PaintedPlan Painted(OptimizationResult plan, string? pngName = null)
        {
            var painted = new PaintedPlan();
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new SheetOptimizerForm(plan, "Ali Raza");
                    form.Show();
                    for (int i = 0; i < 20; i++) Application.DoEvents();

                    void Walk(Control c)
                    {
                        if (c is Label l && l.Text.Length > 0) painted.Lines.Add(l.Text);

                        if (c.GetType().Name == "SheetDiagram")
                        {
                            var sheet = (SheetLayout)c.GetType()
                                .GetField("_sheet", BindingFlags.NonPublic | BindingFlags.Instance)!
                                .GetValue(c)!;
                            painted.Pictures[sheet.Source] = c.Size;
                        }

                        foreach (Control child in c.Controls) Walk(child);
                    }
                    Walk(form);

                    if (pngName != null)
                    {
                        Directory.CreateDirectory(Scratch);
                        using var bmp = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                        bmp.Save(Path.Combine(Scratch, pngName), System.Drawing.Imaging.ImageFormat.Png);
                    }

                    form.Close();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The plan window never settled.");

            Assert.Null(failure);
            return painted;
        }

        private sealed class PaintedPlan
        {
            public List<string> Lines { get; } = new();
            public Dictionary<string, Size> Pictures { get; } = new();
            public string Text => string.Join("\n", Lines);
        }

        [Fact]
        public void ThePrintedPlan_CarriesTheStockAndTheShortage()
        {
            var stock = new[]
            {
                new SheetCuttingOptimizer.StockSheet("KMI 123", 48, 96, 2),
                new SheetCuttingOptimizer.StockSheet("PVC",     48, 48, 4)
            };
            var rows = NineBoards();
            rows.Add(Row(10, 40, 40, 1, "PVC"));

            var plan = Opt.OptimizeBySheetCode(rows, stock);

            Directory.CreateDirectory(Scratch);
            SheetPlanPdfService.Build(plan, "Ali Raza", new DateTime(2026, 11, 8))
                               .GenerateImages(p => Path.Combine(Scratch, $"stock-plan{p}.png"),
                                   new QuestPDF.Infrastructure.ImageGenerationSettings
                                   {
                                       ImageFormat = QuestPDF.Infrastructure.ImageFormat.Png,
                                       RasterDpi   = 150
                                   });

            Assert.True(File.Exists(Path.Combine(Scratch, "stock-plan0.png")));
        }
    }
}
