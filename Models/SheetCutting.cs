namespace GreenWoodWorkAging.Models
{
    /// <summary>
    /// One physical piece placed on a sheet. Every number is in eighths of an inch
    /// (sooter) measured from the sheet's top-left corner, so a cutting plan is exact
    /// integer arithmetic like the rest of the bill.
    /// </summary>
    public class CutPlacement
    {
        /// <summary>Grid row the piece came from, so the same size always looks the same.</summary>
        public int SerialNumber { get; set; }

        /// <summary>Left edge of the piece, in eighths from the sheet's left edge.</summary>
        public int X { get; set; }

        /// <summary>Top edge of the piece, in eighths from the sheet's top edge.</summary>
        public int Y { get; set; }

        /// <summary>Edge running along the sheet's width, after any 90° turn.</summary>
        public int WidthEighths { get; set; }

        /// <summary>Edge running along the sheet's height, after any 90° turn.</summary>
        public int HeightEighths { get; set; }

        /// <summary>True when the piece is turned 90° from how it was typed.</summary>
        public bool Rotated { get; set; }

        public int Area => WidthEighths * HeightEighths;
        public int Right  => X + WidthEighths;
        public int Bottom => Y + HeightEighths;
    }

    /// <summary>A rectangle of plywood left uncut on a sheet.</summary>
    public class ScrapRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int WidthEighths { get; set; }
        public int HeightEighths { get; set; }

        public int Area => WidthEighths * HeightEighths;
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

        public int SheetWidthEighths { get; set; }
        public int SheetHeightEighths { get; set; }

        public List<CutPlacement> Placements { get; } = new();
        public List<ScrapRect> Scrap { get; } = new();

        public int SheetArea => SheetWidthEighths * SheetHeightEighths;
        public int UsedArea  => Placements.Sum(p => p.Area);

        /// <summary>Share of the sheet that becomes a piece rather than scrap.</summary>
        public double UsedPercent => SheetArea == 0 ? 0 : UsedArea * 100.0 / SheetArea;
    }

    /// <summary>A piece that cannot be cut from a single sheet at all.</summary>
    public class UnplacedPiece
    {
        public int SerialNumber { get; set; }
        public int HeightEighths { get; set; }
        public int WidthEighths { get; set; }
    }

    /// <summary>The whole cutting plan for one bill.</summary>
    public class OptimizationResult
    {
        public int SheetWidthEighths { get; set; }
        public int SheetHeightEighths { get; set; }

        public List<SheetLayout> Sheets { get; } = new();
        public List<UnplacedPiece> Unplaced { get; } = new();

        public int PieceCount => Sheets.Sum(s => s.Placements.Count) + Unplaced.Count;

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
