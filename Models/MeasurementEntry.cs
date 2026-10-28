namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Represents a single carpenter measurement row with height, width, and quantity.
    /// All measurements are stored internally as eighth-inch units (sooter units) to
    /// guarantee exact integer arithmetic — no floating-point rounding ever occurs.
    /// </summary>
    public class MeasurementEntry
    {
        /// <summary>Serial number displayed to the user (1-based).</summary>
        public int SerialNumber { get; set; }

        /// <summary>Height in whole inches.</summary>
        public int HeightInches { get; set; }

        /// <summary>Height fractional part in sooter (0–7). 1 inch = 8 sooter.</summary>
        public int HeightSooter { get; set; }

        /// <summary>Width in whole inches.</summary>
        public int WidthInches { get; set; }

        /// <summary>Width fractional part in sooter (0–7).</summary>
        public int WidthSooter { get; set; }

        /// <summary>Number of identical pieces (must be ≥ 1).</summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Height expressed as eighth-inch units.
        /// HeightInches * 8 + HeightSooter
        /// </summary>
        public int HeightEighths => HeightInches * 8 + HeightSooter;

        /// <summary>
        /// Width expressed as eighth-inch units.
        /// WidthInches * 8 + WidthSooter
        /// </summary>
        public int WidthEighths => WidthInches * 8 + WidthSooter;

        /// <summary>
        /// Perimeter of one piece in eighth-inch units.
        /// Formula: Height + Height + Width + Width  (carpenter's four-sides rule)
        /// </summary>
        public int OnePieceEighths => HeightEighths + HeightEighths + WidthEighths + WidthEighths;

        /// <summary>
        /// Total measurement for this row in eighth-inch units.
        /// Formula: OnePiece × Quantity
        /// </summary>
        public int RowTotalEighths => OnePieceEighths * Quantity;
    }
}
