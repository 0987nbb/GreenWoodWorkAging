namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Holds the computed grand totals across all measurement rows.
    /// All values are kept in exact integer sixteenth-inch units until the
    /// final conversion to feet/inches/sooter for display purposes.
    /// </summary>
    public class GrandTotalResult
    {
        /// <summary>Number of measurement rows in the list.</summary>
        public int TotalRows { get; set; }

        /// <summary>Sum of (Quantity) for every row — total number of pieces.</summary>
        public int TotalPieces { get; set; }

        /// <summary>Sum of all RowTotalSixteenths across every row.</summary>
        public int GrandTotalSixteenths { get; set; }

        // ── Derived convenience properties ──────────────────────────────────

        /// <summary>Whole inches in the grand total.</summary>
        public int GrandTotalWholeInches => GrandTotalSixteenths / Sooter.PerInch;

        /// <summary>Remaining sooter after extracting whole inches (0–7).</summary>
        public int GrandTotalRemainderSooter => (GrandTotalSixteenths % Sooter.PerInch) / Sooter.PerSooter;

        /// <summary>The sady left after the whole sooters — 0 or 1.</summary>
        public int GrandTotalRemainderHalf => GrandTotalSixteenths % Sooter.PerSooter;

        /// <summary>Whole feet (GrandTotalWholeInches / 12).</summary>
        public int GrandTotalFeet => GrandTotalWholeInches / 12;

        /// <summary>Remaining whole inches after extracting feet (GrandTotalWholeInches % 12).</summary>
        public int GrandTotalRemainingInches => GrandTotalWholeInches % 12;
    }
}
