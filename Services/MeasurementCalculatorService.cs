using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Pure calculation service — no UI dependencies.
    /// All arithmetic uses integer sixteenth-inch units to guarantee exact results.
    /// </summary>
    public class MeasurementCalculatorService
    {
        // ── Row calculations ────────────────────────────────────────────────

        /// <summary>
        /// Calculates the perimeter of one piece in sixteenth-inch units.
        /// Carpenter's four-sides rule: H + H + W + W
        /// </summary>
        public int CalculateOnePieceSixteenths(int heightSixteenths, int widthSixteenths)
            => heightSixteenths + heightSixteenths + widthSixteenths + widthSixteenths;

        /// <summary>
        /// Calculates the row total in sixteenth-inch units.
        /// Row total = OnePiece × Quantity
        /// </summary>
        public int CalculateRowTotalSixteenths(int onePieceSixteenths, int quantity)
            => onePieceSixteenths * quantity;

        // ── Grand total calculations ─────────────────────────────────────────

        /// <summary>
        /// Aggregates all measurement rows into a single <see cref="GrandTotalResult"/>.
        /// No rounding is applied until the final display conversion.
        /// </summary>
        public GrandTotalResult CalculateGrandTotal(IEnumerable<MeasurementEntry> entries)
        {
            int totalSixteenths = 0;
            int totalPieces = 0;
            int totalRows = 0;

            foreach (var entry in entries)
            {
                totalSixteenths += entry.RowTotalSixteenths;
                totalPieces += entry.Quantity;
                totalRows++;
            }

            return new GrandTotalResult
            {
                TotalRows = totalRows,
                TotalPieces = totalPieces,
                GrandTotalSixteenths = totalSixteenths
            };
        }

        // ── Conversion helpers ───────────────────────────────────────────────

        /// <summary>
        /// Converts a raw sixteenth-inch value into its component parts: whole inches,
        /// sooters (0–7) and the sady — half a sooter — left over.
        /// </summary>
        public (int wholeInches, int sooter, int half) SixteenthsToParts(int sixteenths)
            => Sooter.Split(sixteenths);

        /// <summary>
        /// Converts a whole-inch count into feet and remaining inches.
        /// </summary>
        public (int feet, int remainingInches) InchesToFeetAndInches(int wholeInches)
        {
            int feet = wholeInches / 12;
            int remaining = wholeInches % 12;
            return (feet, remaining);
        }
    }
}
