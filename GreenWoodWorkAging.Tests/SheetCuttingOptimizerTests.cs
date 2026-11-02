using System.Diagnostics;
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

        /// <summary>
        /// Packed hundreds of ways, the plan must still be physically cuttable: every piece on
        /// the sheet, none sharing plywood, and never fewer sheets than the bill's own area or
        /// its busiest size allows. Random sizes are the worst case for a packer, so they are
        /// what this checks.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(23)]
        [InlineData(64)]
        [InlineData(101)]
        public void RandomBill_PacksLegitimatelyAndNotUnderItsBound(int seed)
        {
            var rng  = new Random(seed);
            var rows = new List<MeasurementEntry>();

            for (int i = 0; i < 1 + rng.Next(8); i++)
                rows.Add(new MeasurementEntry
                {
                    SerialNumber = i + 1,
                    HeightInches = 6 + rng.Next(86), HeightSooter = rng.Next(8),
                    WidthInches  = 3 + rng.Next(20), WidthSooter  = rng.Next(8),
                    Quantity     = 1 + rng.Next(6)
                });

            var plan = Opt.Optimize(rows);

            foreach (var sheet in plan.Sheets)
            {
                Assert.All(sheet.Placements, p =>
                {
                    Assert.True(p.X >= 0 && p.Y >= 0);
                    Assert.True(p.Right <= sheet.SheetWidthEighths);
                    Assert.True(p.Bottom <= sheet.SheetHeightEighths);
                });

                foreach (var a in sheet.Placements)
                    foreach (var b in sheet.Placements)
                        if (!ReferenceEquals(a, b))
                            Assert.True(a.X >= b.Right || b.X >= a.Right || a.Y >= b.Bottom || b.Y >= a.Bottom,
                                $"pieces {a.SerialNumber} and {b.SerialNumber} overlap on sheet {sheet.Number}");

                foreach (var offcut in sheet.Scrap)
                    Assert.All(sheet.Placements, p =>
                        Assert.True(offcut.X >= p.Right || p.X >= offcut.X + offcut.WidthEighths ||
                                    offcut.Y >= p.Bottom || p.Y >= offcut.Y + offcut.HeightEighths));

                Assert.True(sheet.UsedArea + sheet.Scrap.Sum(f => f.Area) <= sheet.SheetArea);
            }

            Assert.Equal(rows.Sum(r => r.Quantity), plan.PieceCount + plan.Unplaced.Count);
            Assert.True(plan.Sheets.Count >= SheetsTheBillCouldNotDoWithLess(rows),
                $"claimed {plan.Sheets.Count} sheets for a bill that needs at least " +
                $"{SheetsTheBillCouldNotDoWithLess(rows)}");
        }

        /// <summary>
        /// The bound a packer can never beat: the bill's area over a sheet, or the sheet's
        /// capacity for its busiest single size.
        /// </summary>
        private static int SheetsTheBillCouldNotDoWithLess(List<MeasurementEntry> rows)
        {
            double sheetArea = (double)SheetHeightEighths * SheetWidthEighths;
            int need = (int)Math.Ceiling(rows.Sum(r => (double)r.HeightEighths * r.WidthEighths * r.Quantity) / sheetArea);

            foreach (var row in rows)
            {
                int longest  = Math.Max(row.HeightEighths, row.WidthEighths);
                int shortest = Math.Min(row.HeightEighths, row.WidthEighths);

                int perSheet = Math.Max(SheetHeightEighths / longest * (SheetWidthEighths / shortest),
                                        SheetHeightEighths / shortest * (SheetWidthEighths / longest));
                if (perSheet > 0) need = Math.Max(need, (int)Math.Ceiling(row.Quantity / (double)perSheet));
            }

            return need;
        }

        /// <summary>
        /// The shop waits for this at a counter, so a crowded bill must stay inside a second
        /// or two even when the search is working through its orders.
        /// </summary>
        [Fact]
        public void CrowdedBill_PacksWithoutHangingTheWindow()
        {
            var rows = Enumerable.Range(1, 14)
                                 .Select(i => Row(i, (8 + i * 6) * 8, (4 + (i * 5) % 22) * 8, 3))
                                 .ToList();

            var clock = Stopwatch.StartNew();
            var plan  = Opt.Optimize(rows);
            clock.Stop();

            Assert.NotEmpty(plan.Sheets);
            Assert.True(clock.ElapsedMilliseconds < 4000, $"took {clock.ElapsedMilliseconds} ms for {plan.PieceCount} pieces");
        }

        /// <summary>Two sheet codes, each with the sizes a shop would actually type.</summary>
        private static List<MeasurementEntry> TwoCodeBill()
        {
            var kmi = new[] { Row(1, 89 * 8, 24 * 8, 1), Row(2, 80 * 8, 18 * 8, 1), Row(3, 60 * 8, 6 * 8, 1),
                              Row(4, 16 * 8 + 2, 10 * 8, 1), Row(5, 36 * 8, 8 * 8, 1), Row(6, 24 * 8, 6 * 8, 2) };
            var glass = new[] { Row(7, 89 * 8, 24 * 8, 1), Row(8, 44 * 8, 22 * 8, 2), Row(9, 30 * 8, 12 * 8, 3) };

            foreach (var r in kmi)   r.SheetCode = "KMI 123";
            foreach (var r in glass) r.SheetCode = "Super Glass";

            var rows = kmi.Concat(glass).ToList();
            return rows;
        }

        [Fact]
        public void PiecesOfDifferentSheetCodes_NeverShareASheet()
        {
            var plan = Opt.OptimizeBySheetCode(TwoCodeBill());

            foreach (var sheet in plan.Sheets)
            {
                var serials = sheet.Placements.Select(p => p.SerialNumber).ToHashSet();
                var expected = TwoCodeBill().Where(r => serials.Contains(r.SerialNumber))
                                            .Select(r => r.SheetCode).Distinct().ToList();
                Assert.Single(expected);
                Assert.Equal(sheet.Source, expected[0]);
            }
        }

        [Fact]
        public void SheetNumbers_RunStraightOnAcrossTheCodes()
        {
            var plan = Opt.OptimizeBySheetCode(TwoCodeBill());

            Assert.Equal(Enumerable.Range(1, plan.Sheets.Count), plan.Sheets.Select(s => s.Number));
            Assert.Equal(2, plan.Sheets.Select(s => s.Source).Distinct().Count());
        }

        /// <summary>A bill that never names a sheet packs exactly the way it always did.</summary>
        [Fact]
        public void UntypedSheetCodes_PackLikeOnePlan()
        {
            var before = Opt.Optimize(SampleBill());
            var after  = Opt.OptimizeBySheetCode(SampleBill());

            Assert.Equal(before.Sheets.Count, after.Sheets.Count);
            Assert.Equal(before.PieceCount,   after.PieceCount);
            Assert.All(after.Sheets, s => Assert.Equal("", s.Source));
        }

        /// <summary>
        /// The bill the shop typed and then checked against an online cut-list tool: five sizes,
        /// eighteen pieces, 1.71 sheets of plywood. Two sheets is what their area needs, and the
        /// online tool found two, so two is what this packer has to do as well.
        /// </summary>
        private static List<MeasurementEntry> TypedBill() => new()
        {
            Row(1, 80 * 8,        18 * 8,        2),    // 80    × 18
            Row(2, 28 * 8 + 4,    17 * 8 + 7,    2),    // 28.4  × 17.7
            Row(3,  8 * 8,        10 * 8 + 3,    2),    // 8     × 10.3
            Row(4, 47 * 8 + 4,    15 * 8 + 2,    2),    // 47.4  × 15.2
            Row(5, 95 * 8,         2 * 8 + 4,   10)     // 95    × 2.4
        };

        [Fact]
        public void TypedBill_UsesNoMoreSheetsThanItsAreaNeeds()
        {
            var plan = Opt.Optimize(TypedBill());

            Assert.Empty(plan.Unplaced);
            Assert.Equal(18, plan.PieceCount);
            Assert.Equal(2, plan.Sheets.Count);
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
