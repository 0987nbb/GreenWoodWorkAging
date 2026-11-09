namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Money charges a customer is billed for on top of the tape measurements.
    /// Amounts use decimal so rupee values never pick up binary rounding error.
    /// </summary>
    public class BillCharges
    {
        /// <summary>Rate charged per foot of tape.</summary>
        public decimal RatePerFoot { get; set; }

        /// <summary>Vehicle (gari) carriage charged once per bill.</summary>
        public decimal CarriageFare { get; set; }

        /// <summary>Number of sheet cuts made for the customer.</summary>
        public int SheetCuts { get; set; }

        /// <summary>Rate charged per sheet cut.</summary>
        public decimal RatePerSheet { get; set; }

        /// <summary>1 foot = 12 inch = 96 sooter = 192 half-sooter units.</summary>
        public static decimal FeetFromSixteenths(int totalSixteenths) => totalSixteenths / (decimal)Sooter.PerFoot;

        /// <summary>Total feet of tape billed, to two decimals.</summary>
        public static decimal Feet(int totalSixteenths) => decimal.Round(FeetFromSixteenths(totalSixteenths), 2);

        public decimal TapeAmount(int totalSixteenths)   => decimal.Round(FeetFromSixteenths(totalSixteenths) * RatePerFoot, 2);
        public decimal SheetAmount                       => SheetCuts * RatePerSheet;
        public decimal TotalAmount(int totalSixteenths)  => TapeAmount(totalSixteenths) + CarriageFare + SheetAmount;

        public bool IsEmpty => RatePerFoot == 0 && CarriageFare == 0 && SheetCuts == 0 && RatePerSheet == 0;

        /// <summary>Rupees on the face of a bill: decimals only when the amount actually has them.</summary>
        public static string Rupees(decimal amount) => "Rs " + amount.ToString("#,##0.##");
    }
}
