using GreenWoodWorkAging.Services;
using Xunit;

namespace GreenWoodWorkAging.Tests
{
    /// <summary>
    /// Unit tests for MeasurementInputValidator — covers the combined "inches.sooter" format.
    /// </summary>
    public class MeasurementInputValidatorTests
    {
        private readonly MeasurementInputValidator _v = new();

        // ── ParseAndValidateMeasurement ──────────────────────────────────────

        [Fact]
        public void Parse_WholeInchesOnly_IsValid()
        {
            var (r, p) = _v.ParseAndValidateMeasurement("80", "Height");
            Assert.True(r.IsValid);
            Assert.NotNull(p);
            Assert.Equal(80, p!.Inches);
            Assert.Equal(0,  p.Sooter);
        }

        [Fact]
        public void Parse_InchesAndSooter_IsValid()
        {
            var (r, p) = _v.ParseAndValidateMeasurement("79.7", "Height");
            Assert.True(r.IsValid);
            Assert.Equal(79, p!.Inches);
            Assert.Equal(7,  p.Sooter);
        }

        [Fact]
        public void Parse_ZeroSooterAfterDot_IsValid()
        {
            var (r, p) = _v.ParseAndValidateMeasurement("80.0", "Width");
            Assert.True(r.IsValid);
            Assert.Equal(80, p!.Inches);
            Assert.Equal(0,  p.Sooter);
        }

        [Fact]
        public void Parse_SooterPart8_IsInvalid()
        {
            var (r, _) = _v.ParseAndValidateMeasurement("79.8", "Height");
            Assert.False(r.IsValid);
            Assert.Contains("sooter", r.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Parse_Empty_IsInvalid()
        {
            var (r, _) = _v.ParseAndValidateMeasurement("", "Height");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void Parse_Letters_IsInvalid()
        {
            var (r, _) = _v.ParseAndValidateMeasurement("abc", "Height");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void Parse_NegativeValue_IsInvalid()
        {
            // Negative not entered via UI (key-filter), but validator should reject
            var (r, _) = _v.ParseAndValidateMeasurement("-5", "Height");
            Assert.False(r.IsValid);
        }

        [Theory]
        [InlineData("17.4", 17, 4)]
        [InlineData("9.0",   9, 0)]
        [InlineData("0.7",   0, 7)]
        [InlineData("100",  100, 0)]
        public void Parse_ValidSamples(string input, int expectedInches, int expectedSooter)
        {
            var (r, p) = _v.ParseAndValidateMeasurement(input, "Test");
            Assert.True(r.IsValid);
            Assert.Equal(expectedInches, p!.Inches);
            Assert.Equal(expectedSooter, p.Sooter);
        }

        // ── ValidateQuantity ─────────────────────────────────────────────────

        [Theory]
        [InlineData("1")]
        [InlineData("10")]
        [InlineData("999")]
        public void ValidateQuantity_Valid(string q)
            => Assert.True(_v.ValidateQuantity(q).IsValid);

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("abc")]
        public void ValidateQuantity_Invalid(string q)
            => Assert.False(_v.ValidateQuantity(q).IsValid);

        // ── ValidateAll ──────────────────────────────────────────────────────

        [Fact]
        public void ValidateAll_AllValid_Succeeds()
        {
            var (r, h, w) = _v.ValidateAll("80", "18", "2");
            Assert.True(r.IsValid);
            Assert.Equal(80, h!.Inches); Assert.Equal(0, h.Sooter);
            Assert.Equal(18, w!.Inches); Assert.Equal(0, w.Sooter);
        }

        [Fact]
        public void ValidateAll_WithSooter_Succeeds()
        {
            var (r, h, w) = _v.ValidateAll("79.7", "17.4", "1");
            Assert.True(r.IsValid);
            Assert.Equal(79, h!.Inches); Assert.Equal(7, h.Sooter);
            Assert.Equal(17, w!.Inches); Assert.Equal(4, w.Sooter);
        }

        [Fact]
        public void ValidateAll_AllZero_Fails()
        {
            var (r, _, _) = _v.ValidateAll("0", "0", "1");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void ValidateAll_BadSooter_Fails()
        {
            var (r, _, _) = _v.ValidateAll("79.8", "17", "1");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void ValidateAll_ZeroQty_Fails()
        {
            var (r, _, _) = _v.ValidateAll("80", "18", "0");
            Assert.False(r.IsValid);
        }
    }
}
