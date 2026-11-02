namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// One line of the sheets a customer hands over at the counter: "10 KMI", "5 Super Glass".
    /// Kept apart from the bill's measurements — a sheet is stock the shop holds, while a
    /// measurement row is a size to cut — so recording stock never touches the billing maths.
    /// </summary>
    public class SheetStockEntry
    {
        /// <summary>Database primary key (0 = not yet saved).</summary>
        public int Id { get; set; }

        /// <summary>The visit this line was recorded in. One visit holds every material.</summary>
        public int VisitId { get; set; }

        /// <summary>The bill the sheets came in with (0 = recorded without a bill).</summary>
        public int BillId { get; set; }

        /// <summary>Who the sheets belong to.</summary>
        public string CustomerName { get; set; } = "";

        /// <summary>Contact number, so the same customer is found again by phone.</summary>
        public string PhoneNumber { get; set; } = "";

        /// <summary>The day the sheets were handed over.</summary>
        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>What the sheets are made of — "KMI", "Super Glass", "PVC", "Textile".</summary>
        public string Material { get; set; } = "";

        /// <summary>How many the customer gave in.</summary>
        public int Received { get; set; }

        /// <summary>How many have been cut since.</summary>
        public int Used { get; set; }

        /// <summary>Still on the shelf. Can read negative if more were cut than were recorded.</summary>
        public int Remaining => Received - Used;
    }

    /// <summary>
    /// One visit to the counter: the customer, the day, and every material they left with their
    /// quantities. A customer with 10 KMI, 5 Super Glass and 3 PVC is one visit holding three
    /// lines — not three separate records — because that is how the shop thinks about it.
    /// </summary>
    public class SheetStockVisit
    {
        /// <summary>Database primary key (0 = not yet saved).</summary>
        public int Id { get; set; }

        /// <summary>The bill open when the sheets came in (0 = none).</summary>
        public int BillId { get; set; }

        public string   CustomerName { get; set; } = "";
        public string   PhoneNumber  { get; set; } = "";
        public DateTime Date         { get; set; } = DateTime.Today;

        /// <summary>One line per material.</summary>
        public List<SheetStockEntry> Lines { get; } = new();

        public int TotalReceived => Lines.Sum(l => l.Received);
        public int TotalUsed     => Lines.Sum(l => l.Used);
        public int TotalLeft     => TotalReceived - TotalUsed;
    }
}
