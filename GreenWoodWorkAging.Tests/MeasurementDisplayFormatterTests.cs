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
        public void FormatEighths_ExactInches_NoFraction()
        {
            Assert.Equal("80\"", _formatter.FormatEighths(80 * 8));
        }

        [Fact]
        public void FormatEighths_WithRemainder_ShowsFraction()
        {
            // 638 eighths = 79 inches + 6 sooter
            Assert.Equal("79-6/8\"", _formatter.FormatEighths(638));
        }

        [Fact]
        public void FormatGrandTotalInches_ExactResult()
        {
            var total = new GrandTotalResult { GrandTotalEighths = 3136 }; // 392 inches
            Assert.Equal("392\"", _formatter.FormatGrandTotalInches(total));
        }

        [Fact]
        public void FormatGrandTotalFeetAndInches_1176Inches()
        {
            var total = new GrandTotalResult { GrandTotalEighths = 1176 * 8 }; // 1176 inches
            string result = _formatter.FormatGrandTotalFeetAndInches(total);
            Assert.Contains("98 ft", result);
            Assert.Contains("0 in", result);
        }

        [Fact]
        public void FormatGrandTotalFeetAndInches_1180Inches()
        {
            var total = new GrandTotalResult { GrandTotalEighths = 1180 * 8 };
            string result = _formatter.FormatGrandTotalFeetAndInches(total);
            Assert.Contains("98 ft", result);
            Assert.Contains("4 in", result);
        }
    }
}
