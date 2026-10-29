namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Which edges of a piece the tape is run along. The numeric values are stored in
    /// the database, so existing rows must keep their meaning: 0 is the original
    /// four-sides rule, 1 and 2 are the single-edge options.
    /// </summary>
    public enum MeasurementSide
    {
        /// <summary>4 sides — H + W + H + W. Default.</summary>
        Both              = 0,
        /// <summary>1 side — Height only.</summary>
        Height            = 1,
        /// <summary>1 side — Width only.</summary>
        Width             = 2,
        /// <summary>2 sides — Height + Width.</summary>
        HeightWidth       = 3,
        /// <summary>3 sides — Height + Height + Width.</summary>
        HeightHeightWidth = 4,
        /// <summary>3 sides — Width + Width + Height.</summary>
        WidthWidthHeight  = 5,
        /// <summary>2 sides — Height + Height.</summary>
        HeightHeight      = 6,
        /// <summary>2 sides — Width + Width.</summary>
        WidthWidth        = 7
    }

    /// <summary>
    /// Represents a single carpenter measurement row with height, width, and quantity.
    /// All measurements are stored internally as eighth-inch units (sooter units) to
    /// guarantee exact integer arithmetic — no floating-point rounding ever occurs.
    /// </summary>
    public class MeasurementEntry
    {
        /// <summary>Database primary key (0 = not yet saved).</summary>
        public int Id { get; set; }

        /// <summary>Foreign key to the parent bill.</summary>
        public int BillId { get; set; }

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

        /// <summary>Which edges the tape is run along. Both = the original four-sides rule.</summary>
        public MeasurementSide Side { get; set; } = MeasurementSide.Both;

        // ── Computed (no storage) ─────────────────────────────────────────────

        /// <summary>How many tape edges run along the height for this selection.</summary>
        public int HeightSides => Side switch
        {
            MeasurementSide.Height            => 1,
            MeasurementSide.Width             => 0,
            MeasurementSide.HeightHeight      => 2,
            MeasurementSide.WidthWidth        => 0,
            MeasurementSide.HeightWidth       => 1,
            MeasurementSide.HeightHeightWidth => 2,
            MeasurementSide.WidthWidthHeight  => 1,
            _                                 => 2
        };

        /// <summary>How many tape edges run along the width for this selection.</summary>
        public int WidthSides => Side switch
        {
            MeasurementSide.Height            => 0,
            MeasurementSide.Width             => 1,
            MeasurementSide.HeightHeight      => 0,
            MeasurementSide.WidthWidth        => 2,
            MeasurementSide.HeightWidth       => 1,
            MeasurementSide.HeightHeightWidth => 1,
            MeasurementSide.WidthWidthHeight  => 2,
            _                                 => 2
        };

        /// <summary>Total tape edges billed per piece (1–4).</summary>
        public int TapeSides => HeightSides + WidthSides;

        public int HeightEighths   => HeightInches * 8 + HeightSooter;
        public int WidthEighths    => WidthInches  * 8 + WidthSooter;

        /// <summary>
        /// Length billed for one piece: the selected edges added together, so the
        /// default stays the original H+H+W+W four-sides rule.
        /// </summary>
        public int OnePieceEighths => (HeightEighths * HeightSides) + (WidthEighths * WidthSides);

        /// <summary>Row total = OnePiece × Quantity.</summary>
        public int RowTotalEighths => OnePieceEighths * Quantity;
    }
}
