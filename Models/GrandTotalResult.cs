namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Holds the computed grand totals across all measurement rows.
    /// All values are kept in exact integer eighth-inch units until the
    /// final conversion to feet/inches/sooter for display purposes.
    /// </summary>
    public class GrandTotalResult
    {
        /// <summary>Number of measurement rows in the list.</summary>
        public int TotalRows { get; set; }

        /// <summary>Sum of (Quantity) for every row — total number of pieces.</summary>
        public int TotalPieces { get; set; }

        /// <summary>Sum of all RowTotalEighths across every row.</summary>
        public int GrandTotalEighths { get; set; }

        // ── Derived convenience properties ──────────────────────────────────

        /// <summary>Whole inches in the grand total (GrandTotalEighths / 8).</summary>
        public int GrandTotalWholeInches => GrandTotalEighths / 8;

        /// <summary>Remaining sooter after extracting whole inches (GrandTotalEighths % 8).</summary>
        public int GrandTotalRemainderSooter => GrandTotalEighths % 8;

        /// <summary>Whole feet (GrandTotalWholeInches / 12).</summary>
        public int GrandTotalFeet => GrandTotalWholeInches / 12;

        /// <summary>Remaining whole inches after extracting feet (GrandTotalWholeInches % 12).</summary>
        public int GrandTotalRemainingInches => GrandTotalWholeInches % 12;
    }
}
