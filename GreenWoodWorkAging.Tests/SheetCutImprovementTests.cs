using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging
{
    public class SheetCutImprovementTests
    {
        private static MeasurementEntry MakeRow(int serial, int heightInches, int widthInches, int qty, string sheetCode = "")
        {
            var h = Sooter.Split(heightInches * Sooter.PerInch);
            var w = Sooter.Split(widthInches * Sooter.PerInch);
            return new MeasurementEntry
            {
                SerialNumber = serial,
                HeightInches = h.inches, HeightSooter = h.sooter, HeightHalf = h.half,
                WidthInches  = w.inches, WidthSooter  = w.sooter, WidthHalf  = w.half,
                Quantity     = qty,
                SheetCode    = sheetCode
            };
        }

        [Fact]
        public void DefaultBladeThickness_Is1Point5Sooter_Equals3Sixteenths()
        {
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: (int)Math.Round(1.5 * Sooter.PerSooter));
            Assert.Equal(3, opt.BladeThicknessSixteenths);

            var rows = new[]
            {
                MakeRow(1, 40, 20, 2)
            };

            var plan = opt.Optimize(rows);
            Assert.Equal(3, plan.BladeThicknessSixteenths);
            Assert.Equal(1.5, plan.BladeThicknessSooter, 2);
            Assert.All(plan.Sheets, s => Assert.Equal(3, s.BladeThicknessSixteenths));
        }

        [Fact]
        public void BladeThickness_IsAppliedToCuts_PiecesSeparatedByAtLeastKerf()
        {
            int kerf = (int)Math.Round(1.5 * Sooter.PerSooter); // 3 sixteenths
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            var rows = new[]
            {
                MakeRow(1, 30, 20, 3),
                MakeRow(2, 25, 15, 2)
            };

            var plan = opt.Optimize(rows);

            Assert.NotEmpty(plan.Sheets);
            foreach (var sheet in plan.Sheets)
            {
                for (int i = 0; i < sheet.Placements.Count; i++)
                {
                    var a = sheet.Placements[i];
                    for (int j = i + 1; j < sheet.Placements.Count; j++)
                    {
                        var b = sheet.Placements[j];

                        bool separatedX = a.X >= b.Right + kerf || b.X >= a.Right + kerf;
                        bool separatedY = a.Y >= b.Bottom + kerf || b.Y >= a.Bottom + kerf;

                        Assert.True(separatedX || separatedY,
                            $"Pieces #{a.SerialNumber} and #{b.SerialNumber} violate blade kerf separation ({kerf} sixteenths) on sheet {sheet.Number}");
                    }
                }
            }
        }

        [Fact]
        public void CustomBladeThickness_CanBeConfigured()
        {
            // 2.0 Suter = 4 sixteenths
            int kerf = (int)Math.Round(2.0 * Sooter.PerSooter);
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            var rows = new[]
            {
                MakeRow(1, 40, 20, 2)
            };

            var plan = opt.Optimize(rows);
            Assert.Equal(4, plan.BladeThicknessSixteenths);
            Assert.Equal(2.0, plan.BladeThicknessSooter, 2);
        }

        [Fact]
        public void PiecesKeepTheirExactRequestedDimensions()
        {
            int kerf = 3;
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            var rows = new[]
            {
                MakeRow(1, 45, 22, 2),
                MakeRow(2, 30, 18, 1)
            };

            var plan = opt.Optimize(rows);

            var placedPiece1 = plan.Sheets.SelectMany(s => s.Placements).Where(p => p.SerialNumber == 1).ToList();
            Assert.Equal(2, placedPiece1.Count);
            Assert.All(placedPiece1, p =>
            {
                int actualW = p.Rotated ? p.HeightSixteenths : p.WidthSixteenths;
                int actualH = p.Rotated ? p.WidthSixteenths : p.HeightSixteenths;
                Assert.Equal(22 * Sooter.PerInch, actualW);
                Assert.Equal(45 * Sooter.PerInch, actualH);
            });
        }

        [Fact]
        public void MinimumWaste_PiecesThatFitOnOneSheet_DoNotOpenSecondSheet()
        {
            int kerf = 3;
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            // Three small pieces easily fitting on a standard 96x48 sheet
            var rows = new[]
            {
                MakeRow(1, 30, 20, 1),
                MakeRow(2, 40, 20, 1),
                MakeRow(3, 20, 20, 1)
            };

            var plan = opt.Optimize(rows);

            Assert.Single(plan.Sheets);
            Assert.Empty(plan.Unplaced);
            Assert.Equal(3, plan.Sheets[0].Placements.Count);
        }

        [Fact]
        public void SheetValidation_RejectsOutOfBounds()
        {
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch
            };

            // Negative X
            var p1 = new CutPlacement { X = -1, Y = 0, WidthSixteenths = 100, HeightSixteenths = 100 };
            var r1 = SheetValidationService.ValidatePlacement(sheet, p1, null, 3);
            Assert.False(r1.IsValid);

            // Exceeds Right
            var p2 = new CutPlacement { X = sheet.SheetWidthSixteenths - 50, Y = 0, WidthSixteenths = 100, HeightSixteenths = 100 };
            var r2 = SheetValidationService.ValidatePlacement(sheet, p2, null, 3);
            Assert.False(r2.IsValid);

            // Exceeds Bottom
            var p3 = new CutPlacement { X = 0, Y = sheet.SheetHeightSixteenths - 50, WidthSixteenths = 100, HeightSixteenths = 100 };
            var r3 = SheetValidationService.ValidatePlacement(sheet, p3, null, 3);
            Assert.False(r3.IsValid);
        }

        [Fact]
        public void SheetValidation_RejectsOverlap()
        {
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch
            };

            var existing = new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 };
            sheet.Placements.Add(existing);

            // Overlaps directly
            var candidate = new CutPlacement { Id = "P2", SerialNumber = 2, X = 50, Y = 50, WidthSixteenths = 100, HeightSixteenths = 100 };
            var res = SheetValidationService.ValidatePlacement(sheet, candidate, candidate.Id, 3);
            Assert.False(res.IsValid);
            Assert.Contains("Overlaps piece #1", res.Reason);
        }

        [Fact]
        public void SheetValidation_RejectsBladeKerfViolation()
        {
            int kerf = 3;
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch
            };

            var existing = new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 };
            sheet.Placements.Add(existing);

            // Adjacent in X, but gap is 1 sixteenth (< kerf of 3)
            var candidate = new CutPlacement { Id = "P2", SerialNumber = 2, X = 201, Y = 0, WidthSixteenths = 100, HeightSixteenths = 200 };
            var res = SheetValidationService.ValidatePlacement(sheet, candidate, candidate.Id, kerf);
            Assert.False(res.IsValid);
            Assert.Contains("blade thickness", res.Reason);
        }

        [Fact]
        public void SheetValidation_AcceptsValidPlacementWithKerf()
        {
            int kerf = 3;
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch
            };

            var existing = new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 };
            sheet.Placements.Add(existing);

            // Exactly kerf (3) apart
            var candidate = new CutPlacement { Id = "P2", SerialNumber = 2, X = 203, Y = 0, WidthSixteenths = 100, HeightSixteenths = 200 };
            var res = SheetValidationService.ValidatePlacement(sheet, candidate, candidate.Id, kerf);
            Assert.True(res.IsValid);
        }

        [Fact]
        public void SheetValidation_SnapPosition_SnapsToBoundaryAndNeighbor()
        {
            int kerf = 3;
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch
            };

            var existing = new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 };
            sheet.Placements.Add(existing);

            // Snap near origin
            var (sx1, sy1) = SheetValidationService.SnapPosition(sheet, 6, 8, 100, 100, "P2", kerf, snapTolerance: 16);
            Assert.Equal(0, sx1);
            Assert.Equal(0, sy1);

            // Snap near neighbor + kerf (203)
            var (sx2, sy2) = SheetValidationService.SnapPosition(sheet, 206, 2, 100, 100, "P2", kerf, snapTolerance: 16);
            Assert.Equal(203, sx2);
            Assert.Equal(0, sy2);
        }

        [Fact]
        public void ManualDragAndDrop_MovingPieceAcrossSheets_UpdatesPlacementsAndScrap()
        {
            int kerf = 3;
            var sheet1 = new SheetLayout
            {
                Number = 1,
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch,
                BladeThicknessSixteenths = kerf
            };
            var sheet2 = new SheetLayout
            {
                Number = 2,
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch,
                BladeThicknessSixteenths = kerf
            };

            var p1 = new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 };
            var p2 = new CutPlacement { Id = "P2", SerialNumber = 2, X = 0, Y = 0, WidthSixteenths = 150, HeightSixteenths = 150 };

            sheet1.Placements.Add(p1);
            sheet2.Placements.Add(p2);

            SheetValidationService.RecomputeScrap(sheet1, kerf);
            SheetValidationService.RecomputeScrap(sheet2, kerf);

            // User moves p2 to sheet 1 at (203, 0)
            p2.X = 203;
            p2.Y = 0;

            var val = SheetValidationService.ValidatePlacement(sheet1, p2, p2.Id, kerf);
            Assert.True(val.IsValid);

            // Move
            sheet2.Placements.Remove(p2);
            sheet1.Placements.Add(p2);

            SheetValidationService.RecomputeScrap(sheet1, kerf);
            SheetValidationService.RecomputeScrap(sheet2, kerf);

            Assert.Equal(2, sheet1.Placements.Count);
            Assert.Empty(sheet2.Placements);

            // Sheet 2 scrap covers entire sheet
            Assert.Single(sheet2.Scrap);
            Assert.Equal(sheet2.SheetArea, sheet2.Scrap[0].Area);
        }

        [Fact]
        public void PdfGeneration_SynchronizesWithManuallyAdjustedLayout()
        {
            int kerf = 3;
            var plan = new OptimizationResult
            {
                SheetWidthSixteenths = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch,
                BladeThicknessSixteenths = kerf
            };

            var sheet1 = new SheetLayout
            {
                Number = 1,
                SheetWidthSixteenths = plan.SheetWidthSixteenths,
                SheetHeightSixteenths = plan.SheetHeightSixteenths,
                BladeThicknessSixteenths = kerf
            };
            sheet1.Placements.Add(new CutPlacement { Id = "P1", SerialNumber = 1, X = 0, Y = 0, WidthSixteenths = 200, HeightSixteenths = 200 });
            sheet1.Placements.Add(new CutPlacement { Id = "P2", SerialNumber = 2, X = 203, Y = 0, WidthSixteenths = 150, HeightSixteenths = 150 });
            SheetValidationService.RecomputeScrap(sheet1, kerf);

            plan.Sheets.Add(sheet1);

            // Build PDF document
            var doc = SheetPlanPdfService.Build(plan, "Test Customer", DateTime.Today);
            Assert.NotNull(doc);
        }

        [Fact]
        public void ExactBoundaryPiece_WithBladeThickness_FitsOnStandardSheet()
        {
            // Standard sheet is 48" x 96". A piece of 48" x 40" should fit without kerf penalty on the 48" boundary.
            int kerf = 3; // 1.5 Suter
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            var rows = new[]
            {
                MakeRow(1, 40, 48, 1)
            };

            var plan = opt.Optimize(rows);
            Assert.Single(plan.Sheets);
            Assert.Empty(plan.Unplaced);

            var placement = plan.Sheets[0].Placements[0];
            int physicalW = placement.Rotated ? placement.HeightSixteenths : placement.WidthSixteenths;
            int physicalH = placement.Rotated ? placement.WidthSixteenths : placement.HeightSixteenths;
            Assert.Equal(48 * Sooter.PerInch, physicalW);
            Assert.Equal(40 * Sooter.PerInch, physicalH);
        }

        [Fact]
        public void MultiplePieces_FittingOnOneSheetWithKerf_NeverOpensSecondSheet()
        {
            int kerf = 3; // 1.5 Suter
            var opt = new SheetCuttingOptimizer(bladeThicknessSixteenths: kerf);

            // Two 47" x 23" pieces on a 48" x 96" sheet easily fit on 1 sheet
            var rows = new[]
            {
                MakeRow(1, 47, 23, 2)
            };

            var plan = opt.Optimize(rows);
            Assert.Single(plan.Sheets);
            Assert.Empty(plan.Unplaced);
            Assert.Equal(2, plan.Sheets[0].Placements.Count);
        }

        [Fact]
        public void RotatePiece_SwapsWidthAndHeight_AndValidatesCorrectly()
        {
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths  = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch,
                BladeThicknessSixteenths = 3
            };

            // Piece 1: 30" tall x 20" wide
            var p1 = new CutPlacement
            {
                Id = "P1",
                SerialNumber = 1,
                X = 0,
                Y = 0,
                WidthSixteenths = 20 * Sooter.PerInch,
                HeightSixteenths = 30 * Sooter.PerInch,
                Rotated = false
            };
            sheet.Placements.Add(p1);

            // Rotate p1 in-place: width becomes 30", height becomes 20"
            int newW = p1.HeightSixteenths;
            int newH = p1.WidthSixteenths;
            var rotatedCandidate = new CutPlacement
            {
                Id = p1.Id,
                SerialNumber = p1.SerialNumber,
                X = p1.X,
                Y = p1.Y,
                WidthSixteenths = newW,
                HeightSixteenths = newH,
                Rotated = !p1.Rotated
            };

            var val = SheetValidationService.ValidatePlacement(sheet, rotatedCandidate, p1.Id, sheet.BladeThicknessSixteenths);
            Assert.True(val.IsValid);

            // Commit rotation
            p1.WidthSixteenths = newW;
            p1.HeightSixteenths = newH;
            p1.Rotated = true;
            SheetValidationService.RecomputeScrap(sheet, sheet.BladeThicknessSixteenths);

            Assert.Equal(30 * Sooter.PerInch, p1.WidthSixteenths);
            Assert.Equal(20 * Sooter.PerInch, p1.HeightSixteenths);
            Assert.True(p1.Rotated);
        }

        [Fact]
        public void RotatePiece_WhenRotatedExceedsSheetWidth_ValidationFails()
        {
            var sheet = new SheetLayout
            {
                SheetWidthSixteenths  = 48 * Sooter.PerInch,
                SheetHeightSixteenths = 96 * Sooter.PerInch,
                BladeThicknessSixteenths = 3
            };

            // Piece: 60" tall x 40" wide (fits vertically because 40 <= 48 and 60 <= 96)
            var p = new CutPlacement
            {
                Id = "P1",
                SerialNumber = 1,
                X = 0,
                Y = 0,
                WidthSixteenths = 40 * Sooter.PerInch,
                HeightSixteenths = 60 * Sooter.PerInch,
                Rotated = false
            };
            sheet.Placements.Add(p);

            // When rotated: width becomes 60", which exceeds sheet width of 48"
            int rotW = p.HeightSixteenths; // 60"
            int rotH = p.WidthSixteenths;  // 40"
            var rotatedCandidate = new CutPlacement
            {
                Id = p.Id,
                SerialNumber = p.SerialNumber,
                X = 0,
                Y = 0,
                WidthSixteenths = rotW,
                HeightSixteenths = rotH,
                Rotated = true
            };

            var val = SheetValidationService.ValidatePlacement(sheet, rotatedCandidate, p.Id, sheet.BladeThicknessSixteenths);
            Assert.False(val.IsValid);
            Assert.Contains("right edge", val.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
