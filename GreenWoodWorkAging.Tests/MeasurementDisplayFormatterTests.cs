using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Unit tests for MeasurementDisplayFormatter — carpenter notation and summary display.
    /// </summary>
    public class MeasurementDisplayFormatterTests
    {
        private readonly MeasurementDisplayFormatter _formatter = new();

        [Fact]
        public void FormatMeasurement_NoSooter_OmitsFraction()
        {
            Assert.Equal("80\"", _formatter.FormatMeasurement(80, 0));
        }

        [Fact]
        public void FormatMeasurement_WithSooter_ShowsFraction()
        {
            Assert.Equal("79-7/8\"", _formatter.FormatMeasurement(79, 7));
            Assert.Equal("17-4/8\"", _formatter.FormatMeasurement(17, 4));
        }

        [Fact]
        public void FormatSixteenths_ExactInches_NoFraction()
        {
            Assert.Equal("80\"", _formatter.FormatSixteenths(80 * Sooter.PerInch));
        }

        [Fact]
        public void FormatSixteenths_WithRemainder_ShowsFraction()
        {
            // 1276 sixteenths = 79 inches + 6 sooter
            Assert.Equal("79-6/8\"", _formatter.FormatSixteenths(1276));
        }

        [Fact]
        public void FormatGrandTotalInches_ExactResult()
        {
            var total = new GrandTotalResult { GrandTotalSixteenths = 6272 }; // 392 inches
            Assert.Equal("392\"", _formatter.FormatGrandTotalInches(total));
        }

        [Fact]
        public void FormatGrandTotalFeetAndInches_1176Inches()
        {
            var total = new GrandTotalResult { GrandTotalSixteenths = 1176 * Sooter.PerInch }; // 1176 inches
            string result = _formatter.FormatGrandTotalFeetAndInches(total);
            Assert.Contains("98 ft", result);
            Assert.Contains("0 in", result);
        }

        [Fact]
        public void FormatGrandTotalFeetAndInches_1180Inches()
        {
            var total = new GrandTotalResult { GrandTotalSixteenths = 1180 * Sooter.PerInch };
            string result = _formatter.FormatGrandTotalFeetAndInches(total);
            Assert.Contains("98 ft", result);
            Assert.Contains("4 in", result);
        }

        // ── Sady (half a sooter) ─────────────────────────────────────────────

        [Theory]
        [InlineData(8, 0, 1, "8.06")]
        [InlineData(8, 1, 1, "8.19")]
        [InlineData(8, 2, 1, "8.31")]
        [InlineData(8, 3, 1, "8.43")]
        [InlineData(8, 4, 1, "8.56")]
        [InlineData(8, 5, 1, "8.68")]
        [InlineData(8, 6, 1, "8.81")]
        [InlineData(8, 7, 1, "8.94")]
        public void Sady_SeesTheTwoDigitCodeTheShopTypes(int inches, int sooter, int half, string expected)
            => Assert.Equal(expected, _formatter.FormatCompact(inches, sooter, half));

        /// <summary>
        /// A plain sooter is typed as the decimal inch it is on the tape — 2 sooter is ".25", not
        /// ".2" — so nothing on the bill loses a digit the shop read off its own tape.
        /// </summary>
        [Theory]
        [InlineData(8, 1, 0, "8.12")]
        [InlineData(8, 2, 0, "8.25")]
        [InlineData(8, 3, 0, "8.37")]
        [InlineData(8, 4, 0, "8.50")]
        [InlineData(8, 5, 0, "8.62")]
        [InlineData(8, 6, 0, "8.75")]
        [InlineData(8, 7, 0, "8.87")]
        [InlineData(8, 0, 0, "8")]
        public void WithoutASady_TheTypedTwoDigitCodeIsShown(int inches, int sooter, int half, string expected)
            => Assert.Equal(expected, _formatter.FormatCompact(inches, sooter, half));

        [Fact]
        public void Sady_RoundTripsThroughTheCompactCode()
        {
            // Whatever is typed, the number on screen must read back as the same length.
            for (int units = 0; units <= 16 * 12; units++)
            {
                var (inches, sooter, half) = Sooter.Split(units);
                string shown = _formatter.FormatCompact(inches, sooter, half);
                Assert.True(MeasurementInputValidator.TryReadSixteenths(shown, out int read),
                            $"{shown} could not be read back");
                Assert.Equal(units, read);
            }
        }

        [Fact]
        public void Sady_KeepsTheClassicFractionOverSixteenths()
        {
            Assert.Equal("(79-3/16)", _formatter.FormatFractionSub(79, 1, 1));
            Assert.Equal("(79-7/8)",  _formatter.FormatFractionSub(79, 7));
        }
    }
}
