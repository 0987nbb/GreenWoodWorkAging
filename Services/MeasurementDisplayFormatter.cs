using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Formats measurements and totals for display in the carpenter-friendly notation.
    /// Example: 79 inches + 7 sooter → "79-7/8\""
    /// </summary>
    public class MeasurementDisplayFormatter
    {
        /// <summary>
        /// Formats a measurement (whole inches + sooter) in carpenter notation.
        /// If sooter is 0 the fraction is omitted: e.g. "80"" or "79-7/8""
        /// </summary>
        public string FormatMeasurement(int wholeInches, int sooter)
        {
            if (sooter == 0)
                return $"{wholeInches}\"";

            return $"{wholeInches}-{sooter}/8\"";
        }

        /// <summary>
        /// Formats an eighth-inch value into carpenter notation.
        /// e.g. 638 eighths → 79 inches 6 sooter → "79-6/8\""
        /// </summary>
        public string FormatEighths(int eighths)
        {
            int wholeInches = eighths / 8;
            int sooter = eighths % 8;
            return FormatMeasurement(wholeInches, sooter);
        }

        /// <summary>
        /// Formats a grand total in inches (whole number) as feet + remaining inches.
        /// e.g. 1176 → "98 ft 0 in"
        /// </summary>
        public string FormatFeetAndInches(int wholeInches, int remainingInches, int remainderSooter)
        {
            if (remainingInches == 0 && remainderSooter == 0)
                return $"{wholeInches / 12} ft 0 in";

            string inchPart = remainderSooter == 0
                ? $"{remainingInches} in"
                : $"{remainingInches}-{remainderSooter}/8 in";

            return $"{wholeInches / 12} ft {inchPart}";
        }

        /// <summary>
        /// Formats a <see cref="GrandTotalResult"/> feet-and-inches display string.
        /// </summary>
        public string FormatGrandTotalFeetAndInches(GrandTotalResult result)
        {
            return FormatFeetAndInches(
                result.GrandTotalWholeInches,
                result.GrandTotalRemainingInches,
                result.GrandTotalRemainderSooter);
        }

        /// <summary>
        /// Formats a grand total inches value (may include fraction).
        /// e.g. 392 eighths / 8 = 49 inches exactly → "49""
        /// </summary>
        public string FormatGrandTotalInches(GrandTotalResult result)
        {
            if (result.GrandTotalRemainderSooter == 0)
                return $"{result.GrandTotalWholeInches}\"";

            return $"{result.GrandTotalWholeInches}-{result.GrandTotalRemainderSooter}/8\"";
        }
    }
}
