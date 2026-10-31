using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Arranges the bill's pieces on standard plywood sheets — 8 ft high by 4 ft wide by
    /// default — so the shop buys the fewest sheets and keeps the least reusable offcut.
    ///
    /// Only straight, edge-to-edge (guillotine) cuts are produced, because that is what a
    /// panel saw can actually do. Every measurement stays in eighths of an inch.
    ///
    /// A greedy best-fit packer is easily fooled by the order the sizes arrive in, so four
    /// things are done about it:
    ///
    ///  * the bill is packed in many piece orders and the best plan is kept;
    ///  * each piece is tried against every corner of every leftover block, not just the top
    ///    one, which is what keeps the offcuts in one usable piece instead of four slivers;
    ///  * a placement that leaves scrap too narrow to ever be cut from again is penalised;
    ///  * every plan is compacted afterwards — the pieces of the last sheet are lifted out and
    ///    re-housed on the earlier sheets, and if they all fit, that sheet is not bought.
    /// </summary>
    public sealed class SheetCuttingOptimizer
    {
        public const int DefaultSheetHeightInches = 96;   // 8 ft
        public const int DefaultSheetWidthInches  = 48;   // 4 ft

        private const int EighthsPerInch = 8;

        /// <summary>An offcut narrower than this across is firewood, not stock.</summary>
        private const int MinReusableEighths = 4 * EighthsPerInch;

        /// <summary>How strongly dead scrap counts against a placement.</summary>
        private const int DeadWeight = 2;

        private const int ShuffledRestarts = 12;

        private readonly int _sheetH;
        private readonly int _sheetW;

        public SheetCuttingOptimizer(int sheetHeightInches = DefaultSheetHeightInches,
                                     int sheetWidthInches  = DefaultSheetWidthInches)
        {
            _sheetH = sheetHeightInches * EighthsPerInch;
            _sheetW = sheetWidthInches  * EighthsPerInch;
        }

        public OptimizationResult Optimize(IEnumerable<MeasurementEntry> rows)
        {
            var pieces = Expand(rows);

            Plan? best = null;
            foreach (var order in Orders(pieces))
            {
                var plan = Compact(Build(order));
                if (best == null || IsBetter(plan, best)) best = plan;
            }

            best ??= new Plan(new List<OpenSheet>(), new List<Piece>());

            var result = new OptimizationResult
            {
                SheetWidthEighths  = _sheetW,
                SheetHeightEighths = _sheetH
            };

            foreach (var sheet in best.Sheets)
            {
                sheet.Layout.Scrap.AddRange(sheet.Scrap);
                result.Sheets.Add(sheet.Layout);
            }

            foreach (var p in best.Unplaced)
                result.Unplaced.Add(new UnplacedPiece
                {
                    SerialNumber  = p.Serial,
                    HeightEighths = p.H,
                    WidthEighths  = p.W
                });

            return result;
        }

        // ── Packing ──────────────────────────────────────────────────────────

        private Plan Build(List<Piece> order)
        {
            var sheets   = new List<OpenSheet>();
            var unplaced = new List<Piece>();

            foreach (var piece in order)
            {
                if (!FitsOnASheet(piece))
                {
                    unplaced.Add(piece);
                    continue;
                }

                var spot = BestSpotAcross(sheets, piece, out var target);

                if (target == null)
                {
                    target = new OpenSheet(sheets.Count + 1, _sheetW, _sheetH);
                    sheets.Add(target);
                    spot = target.FindSpot(piece);
                }

                target.Place(piece, spot!);
            }

            return new Plan(sheets, unplaced);
        }

        /// <summary>The cheapest position for one piece over every open sheet.</summary>
        private static Spot? BestSpotAcross(List<OpenSheet> sheets, Piece piece, out OpenSheet? target)
        {
            Spot? best = null;
            target = null;

            foreach (var sheet in sheets)
            {
                var spot = sheet.FindSpot(piece);
                if (spot != null && (best == null || spot.BetterThan(best)))
                {
                    best   = spot;
                    target = sheet;
                }
            }

            return best;
        }

        /// <summary>
        /// Lifts the last sheet's pieces out and tries to house them on the sheets before it.
        /// Repeats while sheets keep disappearing, so a bill that only needed one more sheet
        /// because of an unlucky placement stops costing the shop a sheet.
        /// </summary>
        private Plan Compact(Plan plan)
        {
            while (plan.Sheets.Count > 1 && TryEmptyLastSheet(plan)) { }
            return plan;
        }

        private bool TryEmptyLastSheet(Plan plan)
        {
            var keep   = plan.Sheets.Take(plan.Sheets.Count - 1).ToList();
            var moving = plan.Sheets[^1].Placed.Select(p => p.Piece).ToList();

            // The same pieces re-housed in a different order give different answers, so a few
            // orders are tried before the sheet is given up on.
            foreach (var order in new[]
            {
                moving.OrderByDescending(p => p.Area).ToList(),
                moving.OrderBy       (p => p.Area).ToList(),
                moving.OrderByDescending(p => p.Longest).ThenByDescending(p => p.Area).ToList(),
                moving.OrderByDescending(p => p.Shortest).ThenByDescending(p => p.Area).ToList()
            })
            {
                var trial = keep.Select(s => OpenSheet.Rebuild(s, _sheetW, _sheetH)).ToList();
                if (HousingFits(trial, order))
                {
                    plan.Sheets.Clear();
                    plan.Sheets.AddRange(trial);
                    return true;
                }
            }

            return false;
        }

        private static bool HousingFits(List<OpenSheet> sheets, List<Piece> pieces)
        {
            foreach (var piece in pieces)
            {
                var spot = BestSpotAcross(sheets, piece, out var target);
                if (target == null) return false;
                target.Place(piece, spot!);
            }
            return true;
        }

        /// <summary>
        /// Fewer sheets always wins. With the same number of sheets the same total area is
        /// left over either way, so what decides it is how much of that leftover is stock.
        /// </summary>
        private static bool IsBetter(Plan a, Plan b)
        {
            if (a.Sheets.Count != b.Sheets.Count) return a.Sheets.Count < b.Sheets.Count;
            if (a.DeadArea     != b.DeadArea)     return a.DeadArea < b.DeadArea;
            return a.LastSheetUsed > b.LastSheetUsed;
        }

        private bool FitsOnASheet(Piece p) =>
            Math.Min(p.H, p.W) <= _sheetW && Math.Max(p.H, p.W) <= _sheetH;

        private static List<Piece> Expand(IEnumerable<MeasurementEntry> rows)
        {
            var list = new List<Piece>();
            foreach (var row in rows)
            {
                if (row.HeightEighths <= 0 || row.WidthEighths <= 0) continue;
                for (int i = 0; i < row.Quantity; i++)
                    list.Add(new Piece(row.SerialNumber, row.HeightEighths, row.WidthEighths));
            }
            return list;
        }

        /// <summary>
        /// Ways of feeding the pieces to the packer. Which one wins depends on the shapes on
        /// the bill, so all of them are tried and the cheapest plan is kept. The jittered runs
        /// exist because one unlucky size can block an otherwise good order.
        /// </summary>
        private List<List<Piece>> Orders(List<Piece> pieces)
        {
            List<Piece> By(Func<Piece, double> key) =>
                pieces.OrderByDescending(key).ThenByDescending(p => p.Area).ToList();

            double sheetArea = (double)_sheetW * _sheetH;

            var orders = new List<List<Piece>>
            {
                By(p => (double)p.Area),
                By(p => (double)p.Longest),
                By(p => (double)p.H),
                By(p => (double)p.W),
                By(p => (double)p.Shortest),

                // Maximum area × maximum side: long boards are the ones that ruin a sheet.
                By(p => (double)p.Longest * p.Area),

                // Value density: how much of the sheet's span a piece uses for its area.
                // The more efficiently a piece fills the sheet, the earlier it goes down.
                By(p => -Math.Min((p.W / (double)_sheetW + p.H / (double)_sheetH) * sheetArea / p.Area,
                                  (p.H / (double)_sheetW + p.W / (double)_sheetH) * sheetArea / p.Area)),

                pieces.OrderBy(p => p.Area).ThenByDescending(p => p.Longest).ToList()
            };

            for (int seed = 1; seed <= ShuffledRestarts; seed++)
            {
                var rng = new Random(seed * 7919);
                orders.Add(pieces.OrderByDescending(p => p.Area * (0.7 + 0.6 * rng.NextDouble())).ToList());
            }

            return orders;
        }

        // ── Types ────────────────────────────────────────────────────────────

        private readonly record struct Piece(int Serial, int H, int W)
        {
            public int Area     => H * W;
            public int Longest  => Math.Max(H, W);
            public int Shortest => Math.Min(H, W);
        }

        private readonly record struct Rect(int X, int Y, int W, int H)
        {
            public int Area  => W * H;
            public int Right => X + W;
            public int Bottom => Y + H;
        }

        /// <summary>One piece on a sheet, and the block of plywood it was cut out of.</summary>
        private readonly record struct PlacedPiece(Piece Piece, Rect Free, int X, int Y, int W, int H, bool Rotated);

        /// <summary>A piece position inside one free rectangle, with what it leaves behind.</summary>
        private sealed class Spot
        {
            public int FreeIndex { get; }
            public int X { get; }
            public int Y { get; }
            public int W { get; }
            public int H { get; }
            public bool Rotated { get; }

            /// <summary>Area abandoned around this piece, whether reusable or not.</summary>
            public int Leftover { get; }

            /// <summary>Part of that area too narrow to ever be cut from again.</summary>
            public int Dead { get; }

            public int Score => Leftover + DeadWeight * Dead;

            public Spot(int freeIndex, Rect free, int x, int y, int w, int h, bool rotated)
            {
                FreeIndex = freeIndex;
                X = x; Y = y; W = w; H = h;
                Rotated   = rotated;

                Leftover = free.Area - w * h;
                Dead     = Split(free, x, y, w, h).Sum(r => DeadOf(r));
            }

            private static int DeadOf(Rect r) =>
                r.W <= 0 || r.H <= 0 ? 0 : Math.Min(r.W, r.H) < MinReusableEighths ? r.Area : 0;

            /// <summary>
            /// The plywood left in the block after the piece is sawn out of it. Two straight
            /// passes — one full length, one across the remainder — so every leftover stays a
            /// rectangle a panel saw can actually reach.
            /// </summary>
            public static IEnumerable<Rect> Split(Rect free, int x, int y, int w, int h)
            {
                if (x - free.X > 0)
                    yield return new Rect(free.X, free.Y, x - free.X, free.H);
                if (free.Right - (x + w) > 0)
                    yield return new Rect(x + w, free.Y, free.Right - (x + w), free.H);
                if (y - free.Y > 0)
                    yield return new Rect(x, free.Y, w, y - free.Y);
                if (free.Bottom - (y + h) > 0)
                    yield return new Rect(x, y + h, w, free.Bottom - (y + h));
            }

            public bool FitsIn(Rect free) => W <= free.W && H <= free.H;

            /// <summary>
            /// Least dead scrap first, then least scrap overall, then the piece as typed so the
            /// carpenter sees fewer flipped boards.
            /// </summary>
            public bool BetterThan(Spot other)
            {
                if (Score != other.Score)     return Score < other.Score;
                if (Rotated != other.Rotated) return !Rotated;
                return Leftover < other.Leftover;
            }

            public CutPlacement ToPlacement(Piece piece) => new()
            {
                SerialNumber   = piece.Serial,
                X              = X,
                Y              = Y,
                WidthEighths   = W,
                HeightEighths  = H,
                Rotated        = Rotated
            };
        }

        private sealed class OpenSheet
        {
            private readonly List<Rect> _free = new();

            public SheetLayout Layout { get; }

            /// <summary>Everything placed here, in the order it was placed, for a replay.</summary>
            public List<PlacedPiece> Placed { get; } = new();

            public OpenSheet(int number, int widthEighths, int heightEighths)
            {
                Layout = new SheetLayout
                {
                    Number              = number,
                    SheetWidthEighths   = widthEighths,
                    SheetHeightEighths  = heightEighths
                };
                _free.Add(new Rect(0, 0, widthEighths, heightEighths));
            }

            /// <summary>A fresh sheet with the same pieces put back exactly where they were.</summary>
            public static OpenSheet Rebuild(OpenSheet source, int widthEighths, int heightEighths)
            {
                var copy = new OpenSheet(source.Layout.Number, widthEighths, heightEighths);
                foreach (var placed in source.Placed)
                {
                    int index = copy._free.IndexOf(placed.Free);
                    if (index < 0) continue;   // cannot happen: the replay follows the same splits
                    copy.PlaceAt(placed.Piece, placed.Free, index, placed.X, placed.Y, placed.W, placed.H, placed.Rotated);
                }
                return copy;
            }

            public IEnumerable<ScrapRect> Scrap =>
                _free.Where(f => f.Area > 0)
                     .OrderByDescending(f => f.Area)
                     .Select(f => new ScrapRect { X = f.X, Y = f.Y, WidthEighths = f.W, HeightEighths = f.H });

            /// <summary>Offcut left here that is too narrow to keep for a future bill.</summary>
            public int DeadArea => _free.Where(f => Math.Min(f.W, f.H) < MinReusableEighths)
                                        .Sum(f => f.Area);

            /// <summary>
            /// The cheapest place for one piece on this sheet. Every corner of every block is
            /// tried, because a piece pushed into the far corner leaves one wide offcut where
            /// the near corner would have left two unusable strips.
            /// </summary>
            public Spot? FindSpot(Piece piece)
            {
                Spot? best = null;

                for (int i = 0; i < _free.Count; i++)
                {
                    var free = _free[i];

                    foreach (var (w, h, rotated) in new[]
                    {
                        (piece.W, piece.H, false),
                        (piece.H, piece.W, true)
                    })
                    {
                        if (w > free.W || h > free.H) continue;

                        foreach (int x in new[] { free.X, free.Right - w })
                            foreach (int y in new[] { free.Y, free.Bottom - h })
                            {
                                var spot = new Spot(i, free, x, y, w, h, rotated);
                                if (best == null || spot.BetterThan(best)) best = spot;
                            }
                    }
                }

                return best;
            }

            public void Place(Piece piece, Spot spot) =>
                PlaceAt(piece, _free[spot.FreeIndex], spot.FreeIndex, spot.X, spot.Y, spot.W, spot.H, spot.Rotated);

            private void PlaceAt(Piece piece, Rect free, int freeIndex, int x, int y, int w, int h, bool rotated)
            {
                _free.RemoveAt(freeIndex);
                foreach (var left in Spot.Split(free, x, y, w, h))
                    if (left.Area > 0) _free.Add(left);

                Layout.Placements.Add(new CutPlacement
                {
                    SerialNumber  = piece.Serial,
                    X             = x,
                    Y             = y,
                    WidthEighths  = w,
                    HeightEighths = h,
                    Rotated       = rotated
                });
                Placed.Add(new PlacedPiece(piece, free, x, y, w, h, rotated));
            }
        }

        private sealed class Plan
        {
            public List<OpenSheet> Sheets { get; }
            public List<Piece> Unplaced { get; }

            public Plan(List<OpenSheet> sheets, List<Piece> unplaced)
            {
                Sheets   = sheets;
                Unplaced = unplaced;
            }

            public int LastSheetUsed => Sheets.Count == 0 ? 0 : Sheets[^1].Layout.UsedArea;
            public int DeadArea      => Sheets.Sum(s => s.DeadArea);
        }
    }
}
