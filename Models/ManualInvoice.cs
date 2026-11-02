namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// Represents a customer invoice created manually by the shop owner,
    /// recording tape details, sheet cutting details, and carriage charges
    /// without requiring individual piece measurements.
    /// </summary>
    public class ManualInvoice
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Tape Details
        public decimal TotalTapesFeet { get; set; }
        public decimal TapeRate { get; set; }
        public decimal TapeAmount => Math.Round(TotalTapesFeet * TapeRate, 2);

        // Sheet Cutting Details
        public int SheetCuts { get; set; }
        public decimal RatePerSheetCut { get; set; }
        public decimal CuttingAmount => Math.Round(SheetCuts * RatePerSheetCut, 2);

        // Carriage / Transportation Charges
        public decimal CarriageCharges { get; set; }

        // Grand Total
        public decimal GrandTotal => TapeAmount + CuttingAmount + CarriageCharges;

        public string Notes { get; set; } = "";
    }
}
