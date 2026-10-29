using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Unit tests for MeasurementCalculatorService covering the four-sides carpenter formula,
    /// grand total aggregation, feet/inches conversion, and fractional (sooter) arithmetic.
    /// </summary>
    public class MeasurementCalculatorServiceTests
    {
        private readonly MeasurementCalculatorService _service = new();

        // ── One-piece perimeter ──────────────────────────────────────────────

        [Fact]
        public void OnePiece_80x18_Qty2_Returns392Inches()
        {
            // Height 80" = 640 eighths, Width 18" = 144 eighths
            // Perimeter = 640+640+144+144 = 1568 eighths (one piece)
            // RowTotal (qty=2) = 1568*2 = 3136 eighths = 392 inches
            int heightEighths = 80 * 8;
            int widthEighths = 18 * 8;
            int onePiece = _service.CalculateOnePieceEighths(heightEighths, widthEighths);
            int rowTotal = _service.CalculateRowTotalEighths(onePiece, 2);

            Assert.Equal(3136, rowTotal);              // 392 inches × 8 = 3136 eighths
            Assert.Equal(392, rowTotal / 8);           // confirmed: 392 inches
        }

        [Fact]
        public void OnePiece_9x17_4Sooter_Qty1_Returns53Inches()
        {
            // Height: 9 inches = 72 eighths
            // Width: 17-4/8 = 140 eighths
            // Perimeter = 72+72+140+140 = 424 eighths = 53 inches exactly
            int heightEighths = 9 * 8 + 0;
            int widthEighths = 17 * 8 + 4;
            int onePiece = _service.CalculateOnePieceEighths(heightEighths, widthEighths);
            int rowTotal = _service.CalculateRowTotalEighths(onePiece, 1);

            Assert.Equal(424, rowTotal);               // 53 inches × 8 = 424 eighths
            Assert.Equal(53, rowTotal / 8);
            Assert.Equal(0, rowTotal % 8);             // no fractional remainder
        }

        [Fact]
        public void OnePiece_FourSidesFormula_IsEquivalentTo2xPerimeter()
        {
            int h = 79 * 8 + 7;  // 79-7/8"
            int w = 17 * 8 + 7;  // 17-7/8"
            int fourSides = _service.CalculateOnePieceEighths(h, w);
            int twoPerimeter = 2 * (h + w);
            Assert.Equal(twoPerimeter, fourSides);
        }

        // ── Quantity scaling ────────────────────────────────────────────────

        [Fact]
        public void RowTotal_ScalesLinearlyWithQuantity()
        {
            int h = 80 * 8;
            int w = 18 * 8;
            int onePiece = _service.CalculateOnePieceEighths(h, w);

            Assert.Equal(onePiece * 1, _service.CalculateRowTotalEighths(onePiece, 1));
            Assert.Equal(onePiece * 5, _service.CalculateRowTotalEighths(onePiece, 5));
            Assert.Equal(onePiece * 10, _service.CalculateRowTotalEighths(onePiece, 10));
        }

        // ── Grand total ─────────────────────────────────────────────────────

        [Fact]
        public void GrandTotal_MultipleRows_SumsCorrectly()
        {
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 80, HeightSooter = 0, WidthInches = 18, WidthSooter = 0, Quantity = 2 }, // 3136 eighths
                new() { HeightInches = 9,  HeightSooter = 0, WidthInches = 17, WidthSooter = 4, Quantity = 1 }  //  424 eighths
            };

            var result = _service.CalculateGrandTotal(entries);
            Assert.Equal(3560, result.GrandTotalEighths);    // 3136 + 424
            Assert.Equal(445, result.GrandTotalWholeInches); // 3560 / 8
            Assert.Equal(0, result.GrandTotalRemainderSooter);
        }

        [Fact]
        public void GrandTotal_EmptyList_ReturnsZeroTotals()
        {
            var result = _service.CalculateGrandTotal(new List<MeasurementEntry>());
            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.TotalPieces);
            Assert.Equal(0, result.GrandTotalEighths);
        }

        [Fact]
        public void GrandTotal_TotalPieces_SumsAllQuantities()
        {
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 80, WidthInches = 18, Quantity = 3 },
                new() { HeightInches = 9,  WidthInches = 17, Quantity = 5 }
            };
            var result = _service.CalculateGrandTotal(entries);
            Assert.Equal(8, result.TotalPieces);
            Assert.Equal(2, result.TotalRows);
        }

        // ── Feet and inches conversion ───────────────────────────────────────

        [Fact]
        public void FeetConversion_1176Inches_Is98Feet0Inches()
        {
            var (feet, remaining) = _service.InchesToFeetAndInches(1176);
            Assert.Equal(98, feet);
            Assert.Equal(0, remaining);
        }

        [Fact]
        public void FeetConversion_1180Inches_Is98Feet4Inches()
        {
            var (feet, remaining) = _service.InchesToFeetAndInches(1180);
            Assert.Equal(98, feet);
            Assert.Equal(4, remaining);
        }

        [Fact]
        public void FeetConversion_392Inches_Is32Feet8Inches()
        {
            var (feet, remaining) = _service.InchesToFeetAndInches(392);
            Assert.Equal(32, feet);
            Assert.Equal(8, remaining);
        }

        // ── Fractional / sooter arithmetic ──────────────────────────────────

        [Fact]
        public void EighthsToInchesAndSooter_638Eighths_Is79Inches6Sooter()
        {
            var (inches, sooter) = _service.EighthsToInchesAndSooter(638);
            Assert.Equal(79, inches);
            Assert.Equal(6, sooter);
        }

        [Fact]
        public void EighthsToInchesAndSooter_ExactInches_HasZeroSooter()
        {
            var (inches, sooter) = _service.EighthsToInchesAndSooter(80 * 8);
            Assert.Equal(80, inches);
            Assert.Equal(0, sooter);
        }

        [Fact]
        public void NoRounding_FractionalTotalsPreserved()
        {
            // 1-1/8" height + 1-1/8" width → OnePiece = (9+9+9+9) = 36 eighths
            // 1-3/8" height + 1-3/8" width → OnePiece = (11+11+11+11) = 44 eighths
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 1, HeightSooter = 1, WidthInches = 1, WidthSooter = 1, Quantity = 1 },
                new() { HeightInches = 1, HeightSooter = 3, WidthInches = 1, WidthSooter = 3, Quantity = 1 }
            };
            var result = _service.CalculateGrandTotal(entries);
            Assert.Equal(80, result.GrandTotalEighths);  // 36+44 = 80 eighths = 10 inches
            Assert.Equal(10, result.GrandTotalWholeInches);
            Assert.Equal(0, result.GrandTotalRemainderSooter);
        }

        // ── Edit / delete ────────────────────────────────────────────────────

        [Fact]
        public void AfterDeletion_GrandTotalRecalculates()
        {
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 80, WidthInches = 18, Quantity = 2 }, // 3136 eighths
                new() { HeightInches = 9,  WidthInches = 17, Quantity = 1 }  //  416 eighths (no sooter)
            };

            var before = _service.CalculateGrandTotal(entries);
            entries.RemoveAt(1);
            var after = _service.CalculateGrandTotal(entries);

            Assert.Equal(3136, after.GrandTotalEighths);
            Assert.True(before.GrandTotalEighths > after.GrandTotalEighths);
        }

        [Fact]
        public void AfterEdit_GrandTotalReflectsNewValues()
        {
            var entry = new MeasurementEntry { HeightInches = 80, WidthInches = 18, Quantity = 2 };
            var entries = new List<MeasurementEntry> { entry };

            var before = _service.CalculateGrandTotal(entries);

            // Edit: change quantity from 2 to 4 → total should double
            entry.Quantity = 4;
            var after = _service.CalculateGrandTotal(entries);

            Assert.Equal(before.GrandTotalEighths * 2, after.GrandTotalEighths);
        }
    }
}
