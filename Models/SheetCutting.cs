namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// One physical piece placed on a sheet. Every number is in sixteenths of an inch
    /// measured from the sheet's top-left corner, so a cutting plan is exact
    /// integer arithmetic like the rest of the bill.
    /// </summary>
    public class CutPlacement
    {
        /// <summary>Grid row the piece came from, so the same size always looks the same.</summary>
        public int SerialNumber { get; set; }

        /// <summary>Left edge of the piece, in sixteenths from the sheet's left edge.</summary>
        public int X { get; set; }

        /// <summary>Top edge of the piece, in sixteenths from the sheet's top edge.</summary>
        public int Y { get; set; }

        /// <summary>Edge running along the sheet's width, after any 90° turn.</summary>
        public int WidthSixteenths { get; set; }

        /// <summary>Edge running along the sheet's height, after any 90° turn.</summary>
        public int HeightSixteenths { get; set; }

        /// <summary>True when the piece is turned 90° from how it was typed.</summary>
        public bool Rotated { get; set; }

        public int Area => WidthSixteenths * HeightSixteenths;
        public int Right  => X + WidthSixteenths;
        public int Bottom => Y + HeightSixteenths;
    }

    /// <summary>A rectangle of plywood left uncut on a sheet.</summary>
    public class ScrapRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int WidthSixteenths { get; set; }
        public int HeightSixteenths { get; set; }

        public int Area => WidthSixteenths * HeightSixteenths;
    }

    /// <summary>One sheet with the pieces placed on it and the leftovers around them.</summary>
    public class SheetLayout
    {
        /// <summary>1-based, printed as "Sheet 1", "Sheet 2", …</summary>
        public int Number { get; set; }

        /// <summary>
        /// The sheet code this sheet is cut from, taken from the measurement rows. Empty when
        /// the bill never named a sheet, which is how every older bill reads and prints.
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        /// The stock this sheet came out of, as the shop named it before optimising —
        /// "KMI 123", "Super Glass". Empty for a plan built without a stock list.
        /// </summary>
        public string StockName { get; set; } = "";

        public int SheetWidthSixteenths { get; set; }
        public int SheetHeightSixteenths { get; set; }

        public List<CutPlacement> Placements { get; } = new();
        public List<ScrapRect> Scrap { get; } = new();

        public int SheetArea => SheetWidthSixteenths * SheetHeightSixteenths;
        public int UsedArea  => Placements.Sum(p => p.Area);

        /// <summary>Share of the sheet that becomes a piece rather than scrap.</summary>
        public double UsedPercent => SheetArea == 0 ? 0 : UsedArea * 100.0 / SheetArea;
    }

    /// <summary>Why a piece could not be cut.</summary>
    public enum UnplacedReason
    {
        /// <summary>Bigger than a stock sheet in any direction.</summary>
        TooBigForSheet,

        /// <summary>It would fit, but the stock entered for it has run out.</summary>
        OutOfStock
    }

    /// <summary>A piece that could not be placed.</summary>
    public class UnplacedPiece
    {
        public int SerialNumber { get; set; }
        public int HeightSixteenths { get; set; }
        public int WidthSixteenths { get; set; }

        /// <summary>Whether the piece is too big for a sheet, or the stock simply ran out.</summary>
        public UnplacedReason Reason { get; set; } = UnplacedReason.TooBigForSheet;

        /// <summary>The stock it was meant to come out of, when a stock list named one.</summary>
        public string StockName { get; set; } = "";
    }

    /// <summary>
    /// One stock sheet the shop said it has, and how much of it the plan used. This is what
    /// lets the plan say "3 of 5 KMI 123 used" instead of leaving the shop to count cards.
    /// </summary>
    public class StockUsage
    {
        public string Name { get; set; } = "";
        public int WidthInches { get; set; }
        public int HeightInches { get; set; }

        /// <summary>How many the shop has. 0 means "not counted" — any number of sheets.</summary>
        public int Available { get; set; }

        /// <summary>How many this plan cuts.</summary>
        public int Used { get; set; }

        /// <summary>
        /// Pieces this stock could not take because it ran out. 0 when the shop had enough.
        /// </summary>
        public int ShortPieces { get; set; }

        public bool Unlimited => Available <= 0;
    }

    /// <summary>The whole cutting plan for one bill.</summary>
    public class OptimizationResult
    {
        public int SheetWidthSixteenths { get; set; }
        public int SheetHeightSixteenths { get; set; }

        public List<SheetLayout> Sheets { get; } = new();
        public List<UnplacedPiece> Unplaced { get; } = new();

        /// <summary>The stock entered before optimising, filled in with what the plan used.</summary>
        public List<StockUsage> Stock { get; } = new();

        public int PieceCount => Sheets.Sum(s => s.Placements.Count) + Unplaced.Count;

        /// <summary>Pieces that would fit but had no sheet left in the stock entered.</summary>
        public List<UnplacedPiece> Shortfalls =>
            Unplaced.Where(u => u.Reason == UnplacedReason.OutOfStock).ToList();

        public double OverallUsedPercent
        {
            get
            {
                int area = Sheets.Sum(s => s.SheetArea);
                return area == 0 ? 0 : Sheets.Sum(s => s.UsedArea) * 100.0 / area;
            }
        }
    }
}
