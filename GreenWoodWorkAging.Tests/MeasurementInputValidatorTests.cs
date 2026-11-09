using GreenWoodWorkAging.Models;
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
        public void Parse_SooterPart8_CarriesIntoTheNextInch()
        {
            // The shop never types 8 sooter — eight of them make one inch.
            var (r, p) = _v.ParseAndValidateMeasurement("79.8", "Height");
            Assert.True(r.IsValid);
            Assert.Equal(80,  p!.Inches);
            Assert.Equal(0,   p.Sooter);
            Assert.Equal(0,   p.Half);
        }

        [Theory]
        // The plain sooter codes: one digit after the point, exactly as they were always typed.
        [InlineData("8.1",  8, 1, 0)]
        [InlineData("8.2",  8, 2, 0)]
        [InlineData("8.7",  8, 7, 0)]
        // The decimal inches, read as eighths of an inch.
        [InlineData("8.12", 8, 1, 0)]
        [InlineData("8.25", 8, 2, 0)]
        [InlineData("8.37", 8, 3, 0)]
        [InlineData("8.50", 8, 4, 0)]
        [InlineData("8.62", 8, 5, 0)]
        [InlineData("8.75", 8, 6, 0)]
        [InlineData("8.87", 8, 7, 0)]
        // The sady codes: the same decimals, half a sooter over.
        [InlineData("8.19", 8, 1, 1)]
        [InlineData("8.31", 8, 2, 1)]
        [InlineData("8.43", 8, 3, 1)]
        [InlineData("8.56", 8, 4, 1)]
        [InlineData("8.68", 8, 5, 1)]
        [InlineData("8.81", 8, 6, 1)]
        [InlineData("8.94", 8, 7, 1)]
        public void Parse_TapeCodes_ReadTheLengthTheyStandFor(string input, int inches, int sooter, int half)
        {
            var (r, p) = _v.ParseAndValidateMeasurement(input, "Height");
            Assert.True(r.IsValid);
            Assert.Equal(inches, p!.Inches);
            Assert.Equal(sooter, p.Sooter);
            Assert.Equal(half,   p.Half);
        }

        [Fact]
        public void Parse_SadyOfSevenAndAHalf_SitsJustBelowTheNextInch()
        {
            var (r, p) = _v.ParseAndValidateMeasurement("79.94", "Height");
            Assert.True(r.IsValid);
            Assert.Equal(79, p!.Inches);
            Assert.Equal(7,  p.Sooter);
            Assert.Equal(1,  p.Half);
            Assert.Equal(1279, Sooter.Combine(p.Inches, p.Sooter, p.Half));   // one sixteenth short of 80"
        }

        [Fact]
        public void Parse_SixteenthIsReadExactly_NotRoundedToAnEighth()
        {
            Assert.True(MeasurementInputValidator.TryReadSixteenths("8.06", out int units));
            Assert.Equal(129, units);                                 // 8" + ½ sooter, not 8" flat
            Assert.True(MeasurementInputValidator.TryReadSixteenths("8.94", out units));
            Assert.Equal(143, units);                                 // 8" + 7½ sooter, one short of 9"
        }

        [Theory]
        [InlineData("8.123456")]   // too many digits to be a tape reading
        [InlineData("8.1a")]
        [InlineData(".5")]
        public void Parse_UnreadableText_IsRejected(string input)
            => Assert.False(MeasurementInputValidator.TryReadSixteenths(input, out _));

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
            var (r, _, _) = _v.ValidateAll("79.12345", "17", "1");
            Assert.False(r.IsValid);
        }

        [Fact]
        public void ValidateAll_SadyOfEightSooter_CarriesAndPasses()
        {
            var (r, h, w) = _v.ValidateAll("79.8", "17.5", "1");
            Assert.True(r.IsValid);
            Assert.Equal(80, h!.Inches);
            Assert.Equal(17, w!.Inches);
            Assert.Equal(5,  w.Sooter);
            Assert.Equal(0,  w.Half);
        }

        [Fact]
        public void ValidateAll_ZeroQty_Fails()
        {
            var (r, _, _) = _v.ValidateAll("80", "18", "0");
            Assert.False(r.IsValid);
        }
    }
}
