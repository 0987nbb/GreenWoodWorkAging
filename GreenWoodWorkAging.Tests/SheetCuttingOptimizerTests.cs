using System.Drawing;
using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The cutting plan: pieces packed onto 96 × 48 inch sheets with straight cuts only.
    /// </summary>
    public class SheetCuttingOptimizerTests
    {
        private const int SheetHeightEighths = 96 * 8;
        private const int SheetWidthEighths  = 48 * 8;

        private static readonly SheetCuttingOptimizer Opt = new();

        /// <summary>A row written in eighth-inch units so the test maths stays exact.</summary>
        private static MeasurementEntry Row(int serial, int heightEighths, int widthEighths, int qty) => new()
        {
            SerialNumber = serial,
            HeightInches = heightEighths / 8, HeightSooter = heightEighths % 8,
            WidthInches  = widthEighths  / 8, WidthSooter  = widthEighths  % 8,
            Quantity     = qty
        };

        private static List<MeasurementEntry> SampleBill() => new()
        {
            Row(1, 89 * 8,        24 * 8, 1),
            Row(2, 80 * 8,        18 * 8, 1),
            Row(3, 60 * 8,         6 * 8, 1),
            Row(4, 16 * 8 + 2,    10 * 8, 1),
            Row(5, 36 * 8,         8 * 8, 1),
            Row(6, 24 * 8,         6 * 8, 2)
        };

        [Fact]
        public void EmptyBillHasNoSheets()
        {
            var plan = Opt.Optimize(new List<MeasurementEntry>());

            Assert.Empty(plan.Sheets);
            Assert.Empty(plan.Unplaced);
            Assert.Equal(0, plan.PieceCount);
        }

        [Fact]
        public void NarrowPiecesThatSpanTheSheet_AllFitOnOneSheet()
        {
            // Four 96 × 12 boards fill the 48 inch width exactly.
            var plan = Opt.Optimize(new[] { Row(1, 96 * 8, 12 * 8, 4) });

            Assert.Single(plan.Sheets);
            Assert.Equal(4, plan.Sheets[0].Placements.Count);
            Assert.Equal(100, plan.Sheets[0].UsedPercent, 1);
        }

        [Fact]
        public void EightIdenticalBoards_FillOneSheet()
        {
            // 24 × 24: four down the 96 inch height, two across the 48 inch width.
            var plan = Opt.Optimize(new[] { Row(1, 24 * 8, 24 * 8, 8) });

            Assert.Single(plan.Sheets);
            Assert.Equal(8, plan.Sheets[0].Placements.Count);
        }

        [Fact]
        public void QuantityIsExpandedIntoThatManyPieces()
        {
            var plan = Opt.Optimize(new[] { Row(1, 32 * 8, 32 * 8, 6) });

            Assert.Equal(6, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.Equal(2, plan.Sheets.Count);   // three per sheet at most
        }

        [Fact]
        public void PieceBiggerThanASheet_IsReportedAndNeverPlaced()
        {
            var plan = Opt.Optimize(new[] { Row(7, 120 * 8, 30 * 8, 1) });

            Assert.Empty(plan.Sheets);
            var unplaced = Assert.Single(plan.Unplaced);
            Assert.Equal(7, unplaced.SerialNumber);
            Assert.Equal(120 * 8, unplaced.HeightEighths);
        }

        [Fact]
        public void EveryPieceLiesInsideItsSheetAndNoTwoOverlap()
        {
            var rows = new List<MeasurementEntry>
            {
                Row(1, 91 * 8 + 4, 18 * 8, 3),
                Row(2, 64 * 8,     31 * 8, 4),
                Row(3, 27 * 8 + 6, 13 * 8, 9),
                Row(4, 48 * 8,     48 * 8, 2),
                Row(5, 8 * 8,       6 * 8, 30)
            };

            var plan = Opt.Optimize(rows);

            Assert.Empty(plan.Unplaced);
            Assert.Equal(rows.Sum(r => r.Quantity), plan.PieceCount);

            foreach (var sheet in plan.Sheets)
            {
                Assert.Equal(SheetWidthEighths,  sheet.SheetWidthEighths);
                Assert.Equal(SheetHeightEighths, sheet.SheetHeightEighths);

                foreach (var p in sheet.Placements)
                {
                    Assert.True(p.X >= 0 && p.Y >= 0,                      "piece off the sheet's origin");
                    Assert.True(p.Right  <= sheet.SheetWidthEighths,       "piece past the sheet's right edge");
                    Assert.True(p.Bottom <= sheet.SheetHeightEighths,      "piece past the sheet's bottom edge");
                    Assert.True(p.Area == p.WidthEighths * p.HeightEighths);
                }

                foreach (var a in sheet.Placements)
                    foreach (var b in sheet.Placements)
                        if (!ReferenceEquals(a, b))
                            Assert.True(a.X >= b.Right || b.X >= a.Right || a.Y >= b.Bottom || b.Y >= a.Bottom,
                                $"pieces {a.SerialNumber} and {b.SerialNumber} overlap on sheet {sheet.Number}");

                // Scrap must not cover a cut piece either.
                foreach (var s in sheet.Scrap)
                    foreach (var p in sheet.Placements)
                        Assert.True(s.X >= p.Right || p.X >= s.X + s.WidthEighths ||
                                    s.Y >= p.Bottom || p.Y >= s.Y + s.HeightEighths,
                                    "scrap sits on top of a piece");

                Assert.True(sheet.UsedArea + sheet.Scrap.Sum(s => s.Area) <= sheet.SheetArea);
            }
        }

        [Fact]
        public void PlanKeepsThePieceSizesExactly()
        {
            var rows = SampleBill();
            var plan = Opt.Optimize(rows);

            var wanted = rows.SelectMany(r => Enumerable.Repeat((r.SerialNumber, r.HeightEighths * r.WidthEighths), r.Quantity))
                             .OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToList();
            var placed = plan.Sheets.SelectMany(s => s.Placements)
                             .Select(p => (p.SerialNumber, p.WidthEighths * p.HeightEighths))
                             .OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToList();

            Assert.Equal(wanted, placed);
        }

        [Fact]
        public void FewerSheetsThanOnePerPiece()
        {
            var plan = Opt.Optimize(SampleBill());

            Assert.Equal(7, plan.PieceCount);   // six rows, one of them qty 2
            Assert.True(plan.Sheets.Count < plan.PieceCount);
            Assert.True(plan.OverallUsedPercent > 50, $"only {plan.OverallUsedPercent:0.#}% of the sheets used");
        }

        /// <summary>
        /// The floor on sheets is the area divided by a sheet. A packer that only ever puts
        /// the biggest piece in the biggest gap lands above that floor on ordinary bills, and
        /// every sheet over it is plywood the shop bought for nothing.
        /// </summary>
        [Fact]
        public void SizesThatTileExactly_DoNotCostAnExtraSheet()
        {
            // Two 48 × 24 panels and four 24 × 24 squares are exactly one 96 × 48 sheet.
            var plan = Opt.Optimize(new[] { Row(1, 48 * 8, 24 * 8, 2), Row(2, 24 * 8, 24 * 8, 4) });

            Assert.Single(plan.Sheets);
            Assert.Equal(100, plan.Sheets[0].UsedPercent, 1);
        }

        /// <summary>The bill the shop actually ran: seven panels plus ten 96 × 3 strips.</summary>
        private static List<MeasurementEntry> ShopBill() => new()
        {
            Row(1, 31 * 8, 12 * 8, 1), Row(2, 17 * 8 + 6, 14 * 8, 1),
            Row(3, 30 * 8 + 6, 8 * 8 + 6, 1), Row(4, 31 * 8 + 6, 15 * 8 + 6, 1),
            Row(5, 28 * 8, 15 * 8 + 6, 1), Row(6, 28 * 8, 16 * 8, 1),
            Row(7, 37 * 8, 20 * 8 + 2, 1), Row(8, 96 * 8, 3 * 8, 10)
        };

        /// <summary>Ordinary carpentry bills, packed down to the number of sheets their area allows.</summary>
        public static TheoryData<List<MeasurementEntry>, int> RealisticBills => new()
        {
            { ShopBill(), 2 },
            // Six door frames: five 82 × 4 stiles and rails plus a 30 × 4 head.
            {
                Enumerable.Range(1, 6).SelectMany(d => new List<MeasurementEntry>
                {
                    Row(d * 10 + 1, 82 * 8, 4 * 8, 2), Row(d * 10 + 2, 30 * 8, 4 * 8, 1)
                }).ToList(),
                2
            },
            // A shelf unit: sides, shelves and a heap of small drawer fronts.
            {
                new List<MeasurementEntry>
                {
                    Row(1, 48 * 8, 18 * 8, 6), Row(2, 24 * 8, 18 * 8, 8),
                    Row(3, 12 * 8, 9 * 8, 12), Row(4, 6 * 8, 6 * 8, 20)
                },
                3
            }
        };

        [Theory]
        [MemberData(nameof(RealisticBills))]
        public void RealisticBill_UsesNoMoreSheetsThanItsAreaNeeds(List<MeasurementEntry> rows, int floor)
        {
            var plan = Opt.Optimize(rows);

            Assert.Empty(plan.Unplaced);
            Assert.Equal(floor, plan.Sheets.Count);
        }

        /// <summary>
        /// Two plans can use the same number of sheets and still not cost the shop the same:
        /// what matters is whether what is left can be cut from again. A strip 3 inches across
        /// is rubbish; one 20 inches across is next month's drawer bottom.
        /// </summary>
        [Fact]
        public void Leftovers_AreStillWorthKeeping()
        {
            var plan = Opt.Optimize(ShopBill());

            var dead = plan.Sheets.SelectMany(s => s.Scrap)
                             .Where(f => Math.Min(f.WidthEighths, f.HeightEighths) < 4 * 8)
                             .ToList();

            Assert.Empty(dead);
        }

        /// <summary>Writes the plan as a picture so the layout can be checked by eye.</summary>
        [Fact]
        public void Draw_RendersThePlanToAFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gwa-sheets", DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(dir);

            var small = Opt.Optimize(SampleBill());
            Assert.NotEmpty(small.Sheets);
            Save(small, Path.Combine(dir, "plan.png"));

            // A crowded bill: many sizes and quantities, several sheets deep.
            var many = Enumerable.Range(1, 12).Select(i => Row(i, (20 + i * 7) * 8, (5 + i * 3) * 8, 1 + i % 3)).ToList();
            var big = Opt.Optimize(many);
            Assert.True(big.Sheets.Count > 2);
            Save(big, Path.Combine(dir, "plan-many.png"));
        }

        private static void Save(OptimizationResult plan, string path)
        {
            const int cellW = 240, cellH = 460;
            using var art = new Bitmap(cellW * plan.Sheets.Count, cellH);
            using (var g = Graphics.FromImage(art))
            {
                g.Clear(Color.White);
                for (int i = 0; i < plan.Sheets.Count; i++)
                    SheetDiagramRenderer.Draw(g, new Rectangle(i * cellW, 0, cellW, cellH), plan.Sheets[i]);
            }
            art.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            Assert.True(new FileInfo(path).Length > 2000);
        }
    }
}
