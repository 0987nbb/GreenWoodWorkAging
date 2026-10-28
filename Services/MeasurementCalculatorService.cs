using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Pure calculation service — no UI dependencies.
    /// All arithmetic uses integer eighth-inch units to guarantee exact results.
    /// </summary>
    public class MeasurementCalculatorService
    {
        // ── Row calculations ────────────────────────────────────────────────

        /// <summary>
        /// Calculates the perimeter of one piece in eighth-inch units.
        /// Carpenter's four-sides rule: H + H + W + W
        /// </summary>
        public int CalculateOnePieceEighths(int heightEighths, int widthEighths)
            => heightEighths + heightEighths + widthEighths + widthEighths;

        /// <summary>
        /// Calculates the row total in eighth-inch units.
        /// Row total = OnePiece × Quantity
        /// </summary>
        public int CalculateRowTotalEighths(int onePieceEighths, int quantity)
            => onePieceEighths * quantity;

        // ── Grand total calculations ─────────────────────────────────────────

        /// <summary>
        /// Aggregates all measurement rows into a single <see cref="GrandTotalResult"/>.
        /// No rounding is applied until the final display conversion.
        /// </summary>
        public GrandTotalResult CalculateGrandTotal(IEnumerable<MeasurementEntry> entries)
        {
            int totalEighths = 0;
            int totalPieces = 0;
            int totalRows = 0;

            foreach (var entry in entries)
            {
                totalEighths += entry.RowTotalEighths;
                totalPieces += entry.Quantity;
                totalRows++;
            }

            return new GrandTotalResult
            {
                TotalRows = totalRows,
                TotalPieces = totalPieces,
                GrandTotalEighths = totalEighths
            };
        }

        // ── Conversion helpers ───────────────────────────────────────────────

        /// <summary>
        /// Converts a raw eighth-inch value into its component parts.
        /// </summary>
        public (int wholeInches, int sooter) EighthsToInchesAndSooter(int eighths)
        {
            int wholeInches = eighths / 8;
            int sooter = eighths % 8;
            return (wholeInches, sooter);
        }

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
