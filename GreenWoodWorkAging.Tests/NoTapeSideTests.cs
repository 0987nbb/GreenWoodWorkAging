using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// A piece that is cut but carries no tape: it keeps its place on the cutting plan and
    /// contributes nothing to the billed inches.
    /// </summary>
    public class NoTapeSideTests
    {
        private static MeasurementEntry Row(MeasurementSide side, int h = 36, int w = 12, int qty = 2) => new()
        {
            SerialNumber = 1,
            HeightInches = h, WidthInches = w, Quantity = qty,
            Side = side
        };

        /// <summary>The numbers are stored in SQLite, so a renumbered option would silently re-label old bills.</summary>
        [Fact]
        public void NoTape_IsAppendedSoOlderStoredValuesStillMeanTheSame()
        {
            Assert.Equal(0, (int)MeasurementSide.Both);
            Assert.Equal(1, (int)MeasurementSide.Height);
            Assert.Equal(2, (int)MeasurementSide.Width);
            Assert.Equal(7, (int)MeasurementSide.WidthWidth);
            Assert.Equal(8, (int)MeasurementSide.NoTape);
        }

        [Fact]
        public void NoTape_BillsNoInchesButKeepsItsSize()
        {
            var m = Row(MeasurementSide.NoTape);

            Assert.Equal(0, m.TapeSides);
            Assert.Equal(0, m.OnePieceSixteenths);
            Assert.Equal(0, m.RowTotalSixteenths);

            Assert.Equal(36 * Sooter.PerInch, m.HeightSixteenths);
            Assert.Equal(12 * Sooter.PerInch, m.WidthSixteenths);
            Assert.Equal(2,      m.Quantity);
        }

        [Fact]
        public void GrandTotal_SkipsTheUntapedRowButStillCountsItsPieces()
        {
            var rows = new List<MeasurementEntry>
            {
                Row(MeasurementSide.Both,     36, 12, 2),   // (36+12+36+12) × 2 = 192 in
                Row(MeasurementSide.NoTape,   40, 10, 3)
            };

            var totals = new MeasurementCalculatorService().CalculateGrandTotal(rows);

            Assert.Equal(192 * Sooter.PerInch, totals.GrandTotalSixteenths);
            Assert.Equal(5,       totals.TotalPieces);
            Assert.Equal(2,       totals.TotalRows);
        }

        [Fact]
        public void UntapedPieces_StillComeOffTheSheet()
        {
            var rows = new List<MeasurementEntry>
            {
                Row(MeasurementSide.Both,   60, 20, 1),
                Row(MeasurementSide.NoTape, 60, 20, 2)
            };

            var plan = new SheetCuttingOptimizer().Optimize(rows);

            Assert.Equal(3, plan.Sheets.Sum(s => s.Placements.Count));
            Assert.Empty(plan.Unplaced);
        }

        [Fact]
        public void TheGridAndTheBillBothSayNoTape()
            => Assert.Equal("No tape", MeasurementDisplayFormatter.FormatSides(MeasurementSide.NoTape));
    }
}
