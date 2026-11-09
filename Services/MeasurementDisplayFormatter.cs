using GreenWoodWorkAging.Models;
using System.Globalization;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Formats measurements for display.
    /// Primary UI format: dot-notation ("8.25") matching how the user enters values.
    /// Secondary/sub-text: classic carpenter fraction notation (79-7/8).
    ///
    /// A size always reads back with the two digits it was typed with, because that is the code
    /// printed on the tape: 1 sooter is ".12", 1½ sooter is ".19", 4 sooter is ".50". A shop
    /// checks a cut against those digits, so nothing on screen or on paper may shorten them.
    /// </summary>
    public class MeasurementDisplayFormatter
    {
        /// <summary>Renders a number with only the decimals it actually needs (80.00 → "80").</summary>
        public static string TrimZeros(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// The tape's own two digits for a fraction of an inch given in sixteenths — 2 is "12"
        /// (1 sooter), 3 is "19" (1½), 8 is "50" (4 sooter), 15 is "94" (7½).
        /// </summary>
        public static string TapeCode(int units) => units switch
        {
            1  => "06", 2  => "12", 3  => "19", 4  => "25",
            5  => "31", 6  => "37", 7  => "43", 8  => "50",
            9  => "56", 10 => "62", 11 => "68", 12 => "75",
            13 => "81", 14 => "87", 15 => "94",
            _  => ((int)Math.Round(units * 100.0 / Sooter.PerInch)).ToString("00", CultureInfo.InvariantCulture)
        };

        // ── Single-label "Inch Sooter" format (bill display) ─────────────────

        /// <summary>
        /// Returns a single human-readable label.
        /// 79 in + 7 sooter → "79 Inch 7 Sooter";  with a sady → "79 Inch 3½ Sooter"
        /// </summary>
        public string FormatInchSooter(int inches, int sooter, int half = 0)
            => half == 0 ? $"{inches} Inch {sooter} Sooter"
                         : $"{inches} Inch {sooter}½ Sooter";

        /// <summary>
        /// Compact dot label that mirrors how values are typed.
        /// 80 in + 0 sooter → "80"   ·   80 in + 2 sooter → "80.25"   ·   + 1½ → "80.19"
        /// </summary>
        public string FormatCompact(int inches, int sooter, int half = 0)
        {
            int units = sooter * Sooter.PerSooter + half;
            return units == 0 ? inches.ToString(CultureInfo.InvariantCulture)
                              : $"{inches}.{TapeCode(units)}";
        }

        /// <summary>Converts sixteenths then returns the compact dot label.</summary>
        public string FormatCompactFromSixteenths(int sixteenths)
        {
            var (inches, sooter, half) = Sooter.Split(sixteenths);
            return FormatCompact(inches, sooter, half);
        }

        /// <summary>Converts sixteenths then returns "X Inch Y Sooter" label.</summary>
        public string FormatInchSooterFromSixteenths(int sixteenths)
        {
            var (inches, sooter, half) = Sooter.Split(sixteenths);
            return FormatInchSooter(inches, sooter, half);
        }

        // ── Dot format (input / grid primary display) ─────────────────────────

        /// <summary>
        /// Formats in dot notation.  79 in + 7 sooter → "79.87""
        /// If there is no fraction the decimal is omitted:  80 in → "80""
        /// </summary>
        public string FormatDot(int inches, int sooter, int half = 0)
        {
            int units = sooter * Sooter.PerSooter + half;
            return units == 0 ? $"{inches}\"" : $"{inches}.{TapeCode(units)}\"";
        }

        /// <summary>Converts sixteenths → whole + sooter + sady, then formats in dot notation.</summary>
        public string FormatDotSixteenths(int sixteenths)
        {
            var (inches, sooter, half) = Sooter.Split(sixteenths);
            return FormatDot(inches, sooter, half);
        }

        // ── Fraction sub-text (secondary / parenthetical) ────────────────────

        /// <summary>
        /// Returns the classic fraction sub-text.  79 in + 7 sooter → "(79-7/8)"
        /// A sady is shown over sixteenths so the number stays exact: 1½ sooter → "(79-3/16)".
        /// </summary>
        public string FormatFractionSub(int inches, int sooter, int half = 0)
            => half == 0 ? $"({inches}-{sooter}/8)"
                         : $"({inches}-{sooter * Sooter.PerSooter + half}/16)";

        /// <summary>Converts sixteenths then returns fraction sub-text.</summary>
        public string FormatFractionSubSixteenths(int sixteenths)
        {
            var (inches, sooter, half) = Sooter.Split(sixteenths);
            return FormatFractionSub(inches, sooter, half);
        }

        // ── Grand-total dot format ───────────────────────────────────────────

        /// <summary>Grand total in dot format.  e.g. "577.4""</summary>
        public string FormatGrandTotalDot(GrandTotalResult r)
            => FormatDot(r.GrandTotalWholeInches, r.GrandTotalRemainderSooter, r.GrandTotalRemainderHalf);

        /// <summary>Decimal sub-text for grand total.  e.g. "(577.5 in)"</summary>
        public string FormatGrandTotalDecimalSub(GrandTotalResult r)
        {
            double dec = r.GrandTotalSixteenths / (double)Sooter.PerInch;
            return $"({TrimZeros(dec)} in)";
        }

        /// <summary>
        /// Feet and remaining in dot format.
        /// e.g. "48 ft 1.4 in"  or  "98 ft 0 in"
        /// </summary>
        public string FormatGrandTotalFeetDot(GrandTotalResult r)
        {
            int remInches = r.GrandTotalRemainingInches;
            int remSooter = r.GrandTotalRemainderSooter;
            int remHalf   = r.GrandTotalRemainderHalf;

            string inPart = remSooter == 0 && remHalf == 0
                ? $"{remInches} in"
                : $"{FormatCompact(remInches, remSooter, remHalf)} in";

            return $"{r.GrandTotalFeet} ft {inPart}";
        }

        /// <summary>Decimal sub-text for feet total.  e.g. "(48.125 ft)"</summary>
        public string FormatGrandTotalFeetDecimalSub(GrandTotalResult r)
        {
            double ft = r.GrandTotalSixteenths / (double)Sooter.PerInch / 12.0;
            return $"({TrimZeros(Math.Round(ft, 3))} ft)";
        }

        /// <summary>
        /// The tape-sides shorthand shown in the grid, on the printed bill and in the PDF,
        /// so a bill always reads exactly like the screen it came from.
        /// </summary>
        public static string FormatSides(MeasurementSide side) => side switch
        {
            MeasurementSide.NoTape            => "No tape",
            MeasurementSide.Height            => "H",
            MeasurementSide.Width             => "W",
            MeasurementSide.HeightHeight      => "H+H",
            MeasurementSide.HeightWidth       => "H+W",
            MeasurementSide.WidthWidth        => "W+W",
            MeasurementSide.HeightHeightWidth => "H+H+W",
            MeasurementSide.WidthWidthHeight  => "W+W+H",
            _                                 => "H+W+H+W"
        };

        // ── Legacy methods (kept for unit tests) ────────────────────────────

        /// <summary>Legacy carpenter-fraction format.  79 in + 7 sooter → "79-7/8\""</summary>
        public string FormatMeasurement(int wholeInches, int sooter, int half = 0)
        {
            if (sooter == 0 && half == 0) return $"{wholeInches}\"";
            return half == 0 ? $"{wholeInches}-{sooter}/8\""
                             : $"{wholeInches}-{sooter * Sooter.PerSooter + half}/16\"";
        }

        /// <summary>Legacy: convert sixteenths then return fraction format.</summary>
        public string FormatSixteenths(int sixteenths)
        {
            var (wholeInches, sooter, half) = Sooter.Split(sixteenths);
            return FormatMeasurement(wholeInches, sooter, half);
        }

        /// <summary>Legacy feet-and-inches formatter.</summary>
        public string FormatFeetAndInches(int wholeInches, int remainingInches, int remainderSooter, int remainderHalf = 0)
        {
            if (remainingInches == 0 && remainderSooter == 0 && remainderHalf == 0)
                return $"{wholeInches / 12} ft 0 in";

            string inchPart = remainderSooter == 0 && remainderHalf == 0
                ? $"{remainingInches} in"
                : remainderHalf == 0
                    ? $"{remainingInches}-{remainderSooter}/8 in"
                    : $"{remainingInches}-{remainderSooter * Sooter.PerSooter + remainderHalf}/16 in";

            return $"{wholeInches / 12} ft {inchPart}";
        }

        /// <summary>Legacy grand-total feet formatter.</summary>
        public string FormatGrandTotalFeetAndInches(GrandTotalResult result)
            => FormatFeetAndInches(result.GrandTotalWholeInches,
                                    result.GrandTotalRemainingInches,
                                    result.GrandTotalRemainderSooter,
                                    result.GrandTotalRemainderHalf);

        /// <summary>Legacy grand-total inches formatter.</summary>
        public string FormatGrandTotalInches(GrandTotalResult result)
            => FormatMeasurement(result.GrandTotalWholeInches,
                                 result.GrandTotalRemainderSooter,
                                 result.GrandTotalRemainderHalf);
    }
}
