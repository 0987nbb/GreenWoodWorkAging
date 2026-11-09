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
            // Height 80" = 1280 sixteenths, Width 18" = 288 sixteenths
            // Perimeter = 1280+1280+288+288 = 3136 sixteenths (one piece)
            // RowTotal (qty=2) = 3136*2 = 6272 sixteenths = 392 inches
            int heightSixteenths = 80 * Sooter.PerInch;
            int widthSixteenths = 18 * Sooter.PerInch;
            int onePiece = _service.CalculateOnePieceSixteenths(heightSixteenths, widthSixteenths);
            int rowTotal = _service.CalculateRowTotalSixteenths(onePiece, 2);

            Assert.Equal(6272, rowTotal);              // 392 inches × 16 = 6272 sixteenths
            Assert.Equal(392, rowTotal / Sooter.PerInch);   // confirmed: 392 inches
        }

        [Fact]
        public void OnePiece_9x17_4Sooter_Qty1_Returns53Inches()
        {
            // Height: 9 inches = 144 sixteenths
            // Width: 17-4/8 = 280 sixteenths
            // Perimeter = 144+144+280+280 = 848 sixteenths = 53 inches exactly
            int heightSixteenths = 9 * Sooter.PerInch + 0;
            int widthSixteenths = 17 * Sooter.PerInch + 4 * Sooter.PerSooter;
            int onePiece = _service.CalculateOnePieceSixteenths(heightSixteenths, widthSixteenths);
            int rowTotal = _service.CalculateRowTotalSixteenths(onePiece, 1);

            Assert.Equal(848, rowTotal);               // 53 inches × 16 = 848 sixteenths
            Assert.Equal(53, rowTotal / Sooter.PerInch);
            Assert.Equal(0, rowTotal % Sooter.PerInch);   // no fractional remainder
        }

        [Fact]
        public void OnePiece_FourSidesFormula_IsEquivalentTo2xPerimeter()
        {
            int h = Sooter.Combine(79, 7);  // 79-7/8"
            int w = Sooter.Combine(17, 7);  // 17-7/8"
            int fourSides = _service.CalculateOnePieceSixteenths(h, w);
            int twoPerimeter = 2 * (h + w);
            Assert.Equal(twoPerimeter, fourSides);
        }

        // ── Quantity scaling ────────────────────────────────────────────────

        [Fact]
        public void RowTotal_ScalesLinearlyWithQuantity()
        {
            int h = 80 * Sooter.PerInch;
            int w = 18 * Sooter.PerInch;
            int onePiece = _service.CalculateOnePieceSixteenths(h, w);

            Assert.Equal(onePiece * 1, _service.CalculateRowTotalSixteenths(onePiece, 1));
            Assert.Equal(onePiece * 5, _service.CalculateRowTotalSixteenths(onePiece, 5));
            Assert.Equal(onePiece * 10, _service.CalculateRowTotalSixteenths(onePiece, 10));
        }

        // ── Grand total ─────────────────────────────────────────────────────

        [Fact]
        public void GrandTotal_MultipleRows_SumsCorrectly()
        {
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 80, HeightSooter = 0, WidthInches = 18, WidthSooter = 0, Quantity = 2 }, // 6272 sixteenths
                new() { HeightInches = 9,  HeightSooter = 0, WidthInches = 17, WidthSooter = 4, Quantity = 1 }  //  848 sixteenths
            };

            var result = _service.CalculateGrandTotal(entries);
            Assert.Equal(7120, result.GrandTotalSixteenths); // 6272 + 848
            Assert.Equal(445, result.GrandTotalWholeInches); // 7120 / 16
            Assert.Equal(0, result.GrandTotalRemainderSooter);
        }

        [Fact]
        public void GrandTotal_EmptyList_ReturnsZeroTotals()
        {
            var result = _service.CalculateGrandTotal(new List<MeasurementEntry>());
            Assert.Equal(0, result.TotalRows);
            Assert.Equal(0, result.TotalPieces);
            Assert.Equal(0, result.GrandTotalSixteenths);
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
        public void SixteenthsToParts_1276Sixteenths_Is79Inches6Sooter()
        {
            var (inches, sooter, half) = _service.SixteenthsToParts(1276);
            Assert.Equal(79, inches);
            Assert.Equal(6, sooter);
            Assert.Equal(0, half);
        }

        [Fact]
        public void SixteenthsToParts_ExactInches_HasZeroSooter()
        {
            var (inches, sooter, half) = _service.SixteenthsToParts(80 * Sooter.PerInch);
            Assert.Equal(80, inches);
            Assert.Equal(0, sooter);
            Assert.Equal(0, half);
        }

        [Fact]
        public void SixteenthsToParts_HalfSooter_IsKept()
        {
            // 79 inches + 3½ sooter = 79 × 16 + 7 sixteenths
            var (inches, sooter, half) = _service.SixteenthsToParts(Sooter.Combine(79, 3, 1));
            Assert.Equal(79, inches);
            Assert.Equal(3, sooter);
            Assert.Equal(1, half);
        }

        [Fact]
        public void NoRounding_FractionalTotalsPreserved()
        {
            // 1-1/8" height + 1-1/8" width → OnePiece = (18+18+18+18) = 72 sixteenths
            // 1-3/8" height + 1-3/8" width → OnePiece = (22+22+22+22) = 88 sixteenths
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 1, HeightSooter = 1, WidthInches = 1, WidthSooter = 1, Quantity = 1 },
                new() { HeightInches = 1, HeightSooter = 3, WidthInches = 1, WidthSooter = 3, Quantity = 1 }
            };
            var result = _service.CalculateGrandTotal(entries);
            Assert.Equal(160, result.GrandTotalSixteenths);  // 72+88 = 160 sixteenths = 10 inches
            Assert.Equal(10, result.GrandTotalWholeInches);
            Assert.Equal(0, result.GrandTotalRemainderSooter);
        }

        [Fact]
        public void SadyRow_CountsHalfASooterOnEverySide()
        {
            // 8 inch ½ sooter square: one side is 129 sixteenths, so the tape bills 4 × 129 = 516,
            // a quarter inch more than the plain 8-inch square (512) ever bills.
            var entry = new MeasurementEntry
            {
                HeightInches = 8, HeightSooter = 0, HeightHalf = 1,
                WidthInches  = 8, WidthSooter  = 0, WidthHalf  = 1,
                Quantity = 1
            };
            var plain = new MeasurementEntry { HeightInches = 8, WidthInches = 8, Quantity = 1 };

            Assert.Equal(Sooter.Combine(8, 0, 1), entry.HeightSixteenths);
            Assert.Equal(4 * 129, entry.RowTotalSixteenths);
            Assert.Equal(512, plain.RowTotalSixteenths);

            var result = _service.CalculateGrandTotal(new List<MeasurementEntry> { entry });
            Assert.Equal(32, result.GrandTotalWholeInches);
            Assert.Equal(2, result.GrandTotalRemainderSooter);
            Assert.Equal(0, result.GrandTotalRemainderHalf);
        }

        // ── Edit / delete ────────────────────────────────────────────────────

        [Fact]
        public void AfterDeletion_GrandTotalRecalculates()
        {
            var entries = new List<MeasurementEntry>
            {
                new() { HeightInches = 80, WidthInches = 18, Quantity = 2 }, // 6272 sixteenths
                new() { HeightInches = 9,  WidthInches = 17, Quantity = 1 }  //  832 sixteenths (no sooter)
            };

            var before = _service.CalculateGrandTotal(entries);
            entries.RemoveAt(1);
            var after = _service.CalculateGrandTotal(entries);

            Assert.Equal(6272, after.GrandTotalSixteenths);
            Assert.True(before.GrandTotalSixteenths > after.GrandTotalSixteenths);
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

            Assert.Equal(before.GrandTotalSixteenths * 2, after.GrandTotalSixteenths);
        }
    }
}
