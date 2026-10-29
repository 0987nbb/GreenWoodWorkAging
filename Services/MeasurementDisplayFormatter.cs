using GreenWoodWorkAging.Models;
using System.Globalization;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Formats measurements for display.
    /// Primary UI format: dot-notation (79.7") matching how the user enters values.
    /// Secondary/sub-text: classic carpenter fraction notation (79-7/8).
    /// </summary>
    public class MeasurementDisplayFormatter
    {
        /// <summary>Renders a number with only the decimals it actually needs (80.00 → "80").</summary>
        public static string TrimZeros(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        // ── Single-label "Inch Sooter" format (bill display) ─────────────────

        /// <summary>
        /// Returns a single human-readable label.
        /// 79 in + 7 sooter → "79 Inch 7 Sooter"
        /// </summary>
        public string FormatInchSooter(int inches, int sooter)
            => $"{inches} Inch {sooter} Sooter";

        /// <summary>
        /// Compact dot label that mirrors how values are typed.
        /// 80 in + 0 sooter → "80"   ·   80 in + 4 sooter → "80.4"
        /// </summary>
        public string FormatCompact(int inches, int sooter)
            => sooter == 0 ? inches.ToString(CultureInfo.InvariantCulture)
                           : $"{inches}.{sooter}";

        /// <summary>Converts eighths then returns the compact dot label.</summary>
        public string FormatCompactFromEighths(int eighths)
            => FormatCompact(eighths / 8, eighths % 8);

        /// <summary>Converts eighths then returns "X Inch Y Sooter" label.</summary>
        public string FormatInchSooterFromEighths(int eighths)
        {
            int inches = eighths / 8;
            int sooter = eighths % 8;
            return FormatInchSooter(inches, sooter);
        }

        // ── Dot format (input / grid primary display) ─────────────────────────

        /// <summary>
        /// Formats in dot notation.  79 in + 7 sooter → "79.7""
        /// If sooter=0 the decimal is omitted:  80 in → "80""
        /// </summary>
        public string FormatDot(int inches, int sooter)
            => sooter == 0 ? $"{inches}\"" : $"{inches}.{sooter}\"";

        /// <summary>Converts eighths → whole + sooter, then formats in dot notation.</summary>
        public string FormatDotEighths(int eighths)
        {
            int inches = eighths / 8;
            int sooter = eighths % 8;
            return FormatDot(inches, sooter);
        }

        // ── Fraction sub-text (secondary / parenthetical) ────────────────────

        /// <summary>
        /// Returns the classic fraction sub-text.  79 in + 7 sooter → "(79-7/8)"
        /// Always includes the /8 denominator so the carpenter can verify.
        /// </summary>
        public string FormatFractionSub(int inches, int sooter)
            => $"({inches}-{sooter}/8)";

        /// <summary>Converts eighths then returns fraction sub-text.</summary>
        public string FormatFractionSubEighths(int eighths)
        {
            int inches = eighths / 8;
            int sooter = eighths % 8;
            return FormatFractionSub(inches, sooter);
        }

        // ── Grand-total dot format ───────────────────────────────────────────

        /// <summary>Grand total in dot format.  e.g. "577.4""</summary>
        public string FormatGrandTotalDot(GrandTotalResult r)
            => FormatDot(r.GrandTotalWholeInches, r.GrandTotalRemainderSooter);

        /// <summary>Decimal sub-text for grand total.  e.g. "(577.5 in)"</summary>
        public string FormatGrandTotalDecimalSub(GrandTotalResult r)
        {
            double dec = r.GrandTotalEighths / 8.0;
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

            string inPart = remSooter == 0
                ? $"{remInches} in"
                : $"{remInches}.{remSooter} in";

            return $"{r.GrandTotalFeet} ft {inPart}";
        }

        /// <summary>Decimal sub-text for feet total.  e.g. "(48.125 ft)"</summary>
        public string FormatGrandTotalFeetDecimalSub(GrandTotalResult r)
        {
            double ft = r.GrandTotalEighths / 8.0 / 12.0;
            return $"({TrimZeros(Math.Round(ft, 3))} ft)";
        }

        /// <summary>
        /// The tape-sides shorthand shown in the grid, on the printed bill and in the PDF,
        /// so a bill always reads exactly like the screen it came from.
        /// </summary>
        public static string FormatSides(MeasurementSide side) => side switch
        {
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
        public string FormatMeasurement(int wholeInches, int sooter)
        {
            if (sooter == 0) return $"{wholeInches}\"";
            return $"{wholeInches}-{sooter}/8\"";
        }

        /// <summary>Legacy: convert eighths then return fraction format.</summary>
        public string FormatEighths(int eighths)
        {
            int wholeInches = eighths / 8;
            int sooter = eighths % 8;
            return FormatMeasurement(wholeInches, sooter);
        }

        /// <summary>Legacy feet-and-inches formatter.</summary>
        public string FormatFeetAndInches(int wholeInches, int remainingInches, int remainderSooter)
        {
            if (remainingInches == 0 && remainderSooter == 0)
                return $"{wholeInches / 12} ft 0 in";

            string inchPart = remainderSooter == 0
                ? $"{remainingInches} in"
                : $"{remainingInches}-{remainderSooter}/8 in";

            return $"{wholeInches / 12} ft {inchPart}";
        }

        /// <summary>Legacy grand-total feet formatter.</summary>
        public string FormatGrandTotalFeetAndInches(GrandTotalResult result)
            => FormatFeetAndInches(result.GrandTotalWholeInches,
                                    result.GrandTotalRemainingInches,
                                    result.GrandTotalRemainderSooter);

        /// <summary>Legacy grand-total inches formatter.</summary>
        public string FormatGrandTotalInches(GrandTotalResult result)
        {
            if (result.GrandTotalRemainderSooter == 0)
                return $"{result.GrandTotalWholeInches}\"";
            return $"{result.GrandTotalWholeInches}-{result.GrandTotalRemainderSooter}/8\"";
        }
    }
}
