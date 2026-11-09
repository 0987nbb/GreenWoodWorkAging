using System.Diagnostics;
using GreenWoodWorkAging.Models;

namespace GreenWoodWorkAging.Services
{
    /// <summary>
    /// Arranges the bill's pieces on standard plywood sheets — 8 ft high by 4 ft wide by
    /// default — so the shop buys the fewest sheets and keeps the least reusable offcut.
    ///
    /// Only straight, edge-to-edge (guillotine) cuts are produced, because that is what a
    /// panel saw can actually do. Every measurement stays in sixteenths of an inch, so a
    /// size typed with a sady is placed at the length it really is.
    ///
    /// A greedy best-fit packer is easily fooled by the order the sizes arrive in, so five
    /// things are done about it:
    ///
    ///  * the bill is packed in many piece orders and the best plan is kept;
    ///  * each piece is tried against every corner of every leftover block, not just the top
    ///    one, which is what keeps the offcuts in one usable piece instead of four slivers;
    ///  * leftover blocks that meet flush are merged into one wider panel, so a band left under
    ///    two side-by-side panels is seen as a band and not as two half-width strips;
    ///  * a placement that leaves scrap too narrow to ever be cut from again is penalised;
    ///  * every plan is compacted afterwards — the pieces of the last sheet are lifted out and
    ///    re-housed on the earlier sheets, and if they all fit, that sheet is not bought.
    ///
    /// On top of that, the greedy runs are compared with a beam search: at every piece the
    /// strongest few positions on every open sheet — plus a brand new sheet — are all carried
    /// forward as separate partial plans, and the widest ones are kept. Best-fit alone always
    /// wedges a panel into the tightest gap, which is often the wrong gap for the pieces that
    /// come after it; the search is what gets a bill down to the number of sheets its own area
    /// allows. It is capped by a time budget so a long bill still answers promptly.
    /// </summary>
    public sealed class SheetCuttingOptimizer
    {
        public const int DefaultSheetHeightInches = 96;   // 8 ft
        public const int DefaultSheetWidthInches  = 48;   // 4 ft

        private const int SixteenthsPerInch = Sooter.PerInch;

        /// <summary>An offcut narrower than this across is firewood, not stock.</summary>
        private const int MinReusableSixteenths = 4 * SixteenthsPerInch;

        /// <summary>How strongly dead scrap counts against a placement.</summary>
        private const int DeadWeight = 2;

        private const int ShuffledRestarts = 12;

        /// <summary>Positions kept per piece while searching, and partial plans carried forward.</summary>
        private const int BranchesPerPiece = 8;
        private const int BeamWidth        = 64;

        /// <summary>How long one bill may be searched before the greedy answer is kept.</summary>
        private const int SearchBudgetMs = 1500;

        /// <summary>Piece orders worth searching; the rest only feed the greedy runs.</summary>
        private const int SearchedOrders = 2;

        /// <summary>
        /// One sheet the shop says it has, typed before the plan is made: what it is called,
        /// how wide and tall it is, and how many are on the floor. A quantity of 0 means the
        /// shop did not count them, which is how every plan was cut before this question existed.
        /// </summary>
        public sealed record StockSheet(string Name, int WidthInches, int HeightInches, int Quantity = 0)
        {
            /// <summary>The standard 8 ft × 4 ft plywood sheet, with no count against it.</summary>
            public static StockSheet Standard(string name = "") =>
                new(name, DefaultSheetWidthInches, DefaultSheetHeightInches, 0);

            public bool IsStandardSize => WidthInches == DefaultSheetWidthInches &&
                                          HeightInches == DefaultSheetHeightInches;
        }

        private readonly int _sheetH;
        private readonly int _sheetW;

        /// <summary>Sheets this run may open. int.MaxValue when the shop did not say how many.</summary>
        private int _maxSheets;

        public SheetCuttingOptimizer(int sheetHeightInches = DefaultSheetHeightInches,
                                     int sheetWidthInches  = DefaultSheetWidthInches,
                                     int sheetQuantity     = 0)
        {
            _sheetH = sheetHeightInches * SixteenthsPerInch;
            _sheetW = sheetWidthInches  * SixteenthsPerInch;

            // A count of 0 or less is "not counted", so the packer never runs out of sheets.
            _maxSheets = sheetQuantity > 0 ? sheetQuantity : int.MaxValue;
        }

        /// <summary>
        /// A packer set to one stock sheet. <paramref name="sheetsLeft"/> is null when the shop
        /// never counted that stock — and zero when it counted and they are all gone, which is
        /// not the same thing.
        /// </summary>
        private static SheetCuttingOptimizer For(StockSheet stock, int? sheetsLeft)
        {
            var packer = new SheetCuttingOptimizer(stock.HeightInches, stock.WidthInches);
            packer._maxSheets = sheetsLeft ?? int.MaxValue;
            return packer;
        }

        public OptimizationResult Optimize(IEnumerable<MeasurementEntry> rows)
        {
            var pieces = Expand(rows);
            var orders = Orders(pieces);

            Plan? best = null;
            foreach (var order in orders)
            {
                var plan = Compact(Build(order));
                if (best == null || IsBetter(plan, best)) best = plan;
            }

            // The area of the bill spread over whole sheets is the fewest it can ever need;
            // stop searching as soon as a plan reaches it.
            int floor = SheetFloor(pieces);

            var clock = Stopwatch.StartNew();
            foreach (var order in orders.Take(SearchedOrders))
            {
                if (best != null && best.Sheets.Count <= floor) break;
                if (clock.ElapsedMilliseconds > SearchBudgetMs) break;

                var found = Search(order, clock);
                if (found == null) break;          // out of time; the greedy answer stands

                var plan = Compact(found);
                if (best == null || IsBetter(plan, best)) best = plan;
            }

            best ??= new Plan(new List<OpenSheet>(), new List<Piece>(), new List<Piece>());

            var result = new OptimizationResult
            {
                SheetWidthSixteenths  = _sheetW,
                SheetHeightSixteenths = _sheetH
            };

            foreach (var sheet in best.Sheets)
            {
                sheet.Layout.Scrap.AddRange(sheet.Scrap);
                result.Sheets.Add(sheet.Layout);
            }

            foreach (var p in best.Unplaced)
                result.Unplaced.Add(new UnplacedPiece
                {
                    SerialNumber     = p.Serial,
                    HeightSixteenths = p.H,
                    WidthSixteenths  = p.W,
                    Reason           = UnplacedReason.TooBigForSheet
                });

            // A piece that would fit but had no sheet left to go on is the shop's problem to
            // solve — buy more, or cut it from another stock — so it is reported apart.
            foreach (var p in best.Starved)
                result.Unplaced.Add(new UnplacedPiece
                {
                    SerialNumber     = p.Serial,
                    HeightSixteenths = p.H,
                    WidthSixteenths  = p.W,
                    Reason           = UnplacedReason.OutOfStock
                });

            return result;
        }

        /// <summary>
        /// Pack the bill one sheet code at a time, so a piece of "KMI 123" never lands on a
        /// sheet of Super Glass. Sizes that were never given a code form one unnamed group,
        /// and a bill with a single group packs exactly like <see cref="Optimize"/>.
        ///
        /// With a stock list the sizes of the sheets come from it instead of the 8 ft × 4 ft
        /// default, and the count entered is spent as the plan opens sheets: a group whose
        /// stock runs out reports the pieces it could not cut rather than buying a sheet the
        /// shop does not have. Without one, nothing about the plan changes.
        /// </summary>
        public OptimizationResult OptimizeBySheetCode(IEnumerable<MeasurementEntry> rows,
                                                      IReadOnlyList<StockSheet>? stock = null)
        {
            var all = rows.ToList();
            var combined = new OptimizationResult
            {
                SheetWidthSixteenths  = _sheetW,
                SheetHeightSixteenths = _sheetH
            };

            if (stock == null || stock.Count == 0)
            {
                int plain = 1;
                foreach (var group in all.GroupBy(r => r.SheetCode ?? ""))
                {
                    var one = Optimize(group);
                    foreach (var sheet in one.Sheets)
                    {
                        sheet.Number = plain++;
                        sheet.Source = group.Key;
                        combined.Sheets.Add(sheet);
                    }
                    combined.Unplaced.AddRange(one.Unplaced);
                }

                return combined;
            }

            var usages = stock.Select(s => new StockUsage
            {
                Name         = s.Name,
                WidthInches  = s.WidthInches,
                HeightInches = s.HeightInches,
                Available    = s.Quantity
            }).ToList();

            // Sheets still unspent, per stock row. An uncounted row is never spent out.
            var counted   = stock.Select(s => s.Quantity > 0).ToArray();
            var remaining = stock.Select(s => s.Quantity).ToArray();

            int number = 1;
            foreach (var group in all.GroupBy(r => r.SheetCode ?? ""))
            {
                int pick = PickStock(group.Key, stock);
                var sheetStock = stock[pick];

                var packer = For(sheetStock, counted[pick] ? remaining[pick] : null);
                var one    = packer.Optimize(group);

                usages[pick].Used += one.Sheets.Count;
                if (counted[pick])
                    remaining[pick] = Math.Max(0, remaining[pick] - one.Sheets.Count);

                foreach (var sheet in one.Sheets)
                {
                    sheet.Number    = number++;
                    sheet.Source    = group.Key;
                    sheet.StockName = sheetStock.Name;
                    combined.Sheets.Add(sheet);
                }

                foreach (var left in one.Unplaced)
                {
                    left.StockName = sheetStock.Name;
                    combined.Unplaced.Add(left);
                    if (left.Reason == UnplacedReason.OutOfStock) usages[pick].ShortPieces++;
                }
            }

            combined.Stock.AddRange(usages);

            // The first stock row is the one the plan window draws its headline from; a plan
            // cut from several sizes still says so sheet by sheet.
            combined.SheetWidthSixteenths  = stock[0].WidthInches  * SixteenthsPerInch;
            combined.SheetHeightSixteenths = stock[0].HeightInches * SixteenthsPerInch;

            return combined;
        }

        /// <summary>
        /// Which stock row a sheet code should be cut from: the row the shop named after the
        /// same sheet, and failing that the first row — the one a shop fills in when it only
        /// has one kind of plywood on the floor.
        /// </summary>
        private static int PickStock(string sheetCode, IReadOnlyList<StockSheet> stock)
        {
            string code = (sheetCode ?? "").Trim();

            if (code.Length > 0)
                for (int i = 0; i < stock.Count; i++)
                    if (string.Equals(stock[i].Name.Trim(), code, StringComparison.OrdinalIgnoreCase))
                        return i;

            for (int i = 0; i < stock.Count; i++)
                if (stock[i].Name.Trim().Length == 0) return i;

            return 0;
        }

        // ── Packing ──────────────────────────────────────────────────────────

        private Plan Build(List<Piece> order)
        {
            var sheets   = new List<OpenSheet>();
            var unplaced = new List<Piece>();
            var starved  = new List<Piece>();

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
                    // The shop said how many sheets it has. Opening one it does not have would
                    // print a plan the carpenter cannot cut, so the piece is left for them to see.
                    if (sheets.Count >= _maxSheets)
                    {
                        starved.Add(piece);
                        continue;
                    }

                    target = new OpenSheet(sheets.Count + 1, _sheetW, _sheetH);
                    sheets.Add(target);
                    spot = target.FindSpot(piece);
                }

                target.Place(piece, spot!);
            }

            return new Plan(sheets, unplaced, starved);
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
        /// Carries several partial plans through the bill instead of one. Best-fit always
        /// chooses the tightest gap, and the tightest gap for this panel is frequently the gap
        /// the next piece needed, so the strongest few positions on every open sheet — and a
        /// brand new sheet — are each kept as their own plan and only the best few survive each
        /// round. This is what reaches the area floor on bills a single greedy pass misreads.
        /// Returns null when the clock runs out, leaving the greedy answer in charge.
        /// </summary>
        private Plan? Search(List<Piece> order, Stopwatch clock)
        {
            // A handful of big panels is worth searching deep; a bill of a hundred small parts
            // is mostly the same shape repeated, and the greedy pass handles those, so the
            // search is narrowed rather than left to run out of the clock.
            bool deep    = order.Count <= 24;
            int width    = deep ? BeamWidth     : BeamWidth / 2;
            int branches = deep ? BranchesPerPiece : BranchesPerPiece / 2;

            var states   = new List<State> { new State(new List<OpenSheet>(), 0) };
            var unplaced = new List<Piece>();

            foreach (var piece in order)
            {
                if (!FitsOnASheet(piece))
                {
                    unplaced.Add(piece);
                    continue;
                }

                var next = new List<State>();

                foreach (var state in states)
                {
                    foreach (var (sheet, spot) in CandidateSpots(state.Sheets, piece, branches))
                        next.Add(state.PlaceOn(sheet, spot, piece, _sheetW, _sheetH));

                    // A branch that would have to open a sheet the shop does not have is not a
                    // plan, so it is dropped; if every branch dies, the greedy answer stands.
                    if (state.Sheets.Count < _maxSheets)
                        next.Add(state.StartNewSheet(piece, _sheetW, _sheetH));
                }

                if (next.Count == 0) return null;
                states = Prune(next, width);

                if (clock.ElapsedMilliseconds > SearchBudgetMs) return null;
            }

            State? winner = null;
            foreach (var state in states)
            {
                if (winner == null ||
                    state.Sheets.Count < winner.Sheets.Count ||
                    (state.Sheets.Count == winner.Sheets.Count &&
                     (state.Dead < winner.Dead ||
                      (state.Dead == winner.Dead && state.LastSheetUsed > winner.LastSheetUsed))))
                    winner = state;
            }

            return winner?.ToPlan(unplaced);
        }

        /// <summary>The strongest positions for one piece over every open sheet, cheapest first.</summary>
        private static List<(OpenSheet Sheet, Spot Spot)> CandidateSpots(List<OpenSheet> sheets, Piece piece, int limit)
        {
            var all = new List<(OpenSheet, Spot)>();

            foreach (var sheet in sheets)
                foreach (var spot in sheet.Corners(piece))
                    all.Add((sheet, spot));

            all.Sort((a, b) =>
            {
                if (a.Item2.Score != b.Item2.Score)       return a.Item2.Score.CompareTo(b.Item2.Score);
                if (a.Item2.Rotated != b.Item2.Rotated)   return a.Item2.Rotated ? 1 : -1;
                return a.Item2.Leftover.CompareTo(b.Item2.Leftover);
            });

            return all.Take(limit).ToList();
        }

        /// <summary>
        /// The best few partial plans of this round. Plans are ranked the way finished plans
        /// are — fewer sheets, then less dead scrap — but a slice of the beam is reserved for
        /// every sheet count still in play, so a plan one sheet ahead of the pack at this
        /// stage is not thrown away for being a sheet behind.
        /// </summary>
        private static List<State> Prune(List<State> states, int width)
        {
            var keep = new List<State>();
            var seen = new HashSet<string>();

            var groups = states.GroupBy(s => s.Sheets.Count)
                               .OrderBy(g => g.Key)
                               .ToList();

            int each = Math.Max(1, width / groups.Count);

            foreach (var group in groups.OrderBy(g => g.Key))
            {
                var byCost = group.OrderBy(s => s.Dead)
                                  .ThenByDescending(s => s.LastSheetUsed)
                                  .ThenBy(s => s.Signature);

                int taken = 0;
                foreach (var state in byCost)
                {
                    if (taken >= each) break;
                    if (!seen.Add(state.Signature)) continue;
                    keep.Add(state);
                    taken++;
                }
            }

            return keep;
        }

        /// <summary>
        /// The fewest sheets the bill could ever need. Two things set that: the area spread
        /// over whole sheets, and the busiest single size — a sheet only holds so many of one
        /// panel, however the rest of the bill looks. A plan that reaches this number is
        /// finished, so the search stops there instead of burning its clock.
        /// </summary>
        private int SheetFloor(List<Piece> pieces)
        {
            double sheetArea = (double)_sheetW * _sheetH;
            var fit = pieces.Where(FitsOnASheet).ToList();

            int need = (int)Math.Ceiling(fit.Sum(p => (double)p.Area) / sheetArea);

            foreach (var group in fit.GroupBy(p => (Math.Max(p.H, p.W), Math.Min(p.H, p.W))))
            {
                (int longest, int shortest) = group.Key;
                if (shortest <= 0) continue;

                int perSheet = Math.Max(_sheetH / longest * (_sheetW / shortest),
                                        _sheetH / shortest * (_sheetW / longest));
                if (perSheet <= 0) continue;

                need = Math.Max(need, (int)Math.Ceiling(group.Count() / (double)perSheet));
            }

            return need;
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
                if (row.HeightSixteenths <= 0 || row.WidthSixteenths <= 0) continue;
                for (int i = 0; i < row.Quantity; i++)
                    list.Add(new Piece(row.SerialNumber, row.HeightSixteenths, row.WidthSixteenths));
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
        private readonly record struct PlacedPiece(Piece Piece, Rect Free, int FreeIndex, int X, int Y, int W, int H, bool Rotated);

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
                r.W <= 0 || r.H <= 0 ? 0 : Math.Min(r.W, r.H) < MinReusableSixteenths ? r.Area : 0;

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
                WidthSixteenths   = W,
                HeightSixteenths  = H,
                Rotated        = Rotated
            };
        }

        private sealed class OpenSheet
        {
            private readonly List<Rect> _free = new();

            public SheetLayout Layout { get; }

            /// <summary>Everything placed here, in the order it was placed, for a replay.</summary>
            public List<PlacedPiece> Placed { get; } = new();

            public OpenSheet(int number, int widthSixteenths, int heightSixteenths)
            {
                Layout = new SheetLayout
                {
                    Number              = number,
                    SheetWidthSixteenths   = widthSixteenths,
                    SheetHeightSixteenths  = heightSixteenths
                };
                _free.Add(new Rect(0, 0, widthSixteenths, heightSixteenths));
            }

            /// <summary>A fresh sheet with the same pieces put back exactly where they were.</summary>
            public static OpenSheet Rebuild(OpenSheet source, int widthSixteenths, int heightSixteenths)
            {
                var copy = new OpenSheet(source.Layout.Number, widthSixteenths, heightSixteenths);
                foreach (var placed in source.Placed)
                {
                    // The replay follows the same splits and the same merges, so the block each
                    // piece came out of is still where it was.
                    int index = placed.FreeIndex < copy._free.Count && copy._free[placed.FreeIndex] == placed.Free
                        ? placed.FreeIndex
                        : copy._free.IndexOf(placed.Free);
                    if (index < 0) continue;
                    copy.PlaceAt(placed.Piece, placed.Free, index, placed.X, placed.Y, placed.W, placed.H, placed.Rotated);
                }
                return copy;
            }

            public IEnumerable<ScrapRect> Scrap =>
                _free.Where(f => f.Area > 0)
                     .OrderByDescending(f => f.Area)
                     .Select(f => new ScrapRect { X = f.X, Y = f.Y, WidthSixteenths = f.W, HeightSixteenths = f.H });

            /// <summary>Offcut left here that is too narrow to keep for a future bill.</summary>
            public int DeadArea => _free.Where(f => Math.Min(f.W, f.H) < MinReusableSixteenths)
                                        .Sum(f => f.Area);

            /// <summary>
            /// What is still uncut on this sheet. Two plans of a beam round that leave the same
            /// blocks are the same plan to pack, whichever sizes made them, so only one is kept.
            /// </summary>
            public string Signature => string.Join(";", _free.OrderBy(f => f.Y).ThenBy(f => f.X)
                                                              .Select(f => $"{f.X}:{f.Y}x{f.W}x{f.H}"));

            /// <summary>
            /// Every position this piece could take on this sheet: each corner of each free
            /// block, in both orientations. Ranked by <see cref="Spot.BetterThan"/> elsewhere —
            /// the greedy packer wants only the cheapest, the search wants the next few too.
            /// </summary>
            public IEnumerable<Spot> Corners(Piece piece)
            {
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
                                yield return new Spot(i, free, x, y, w, h, rotated);
                    }
                }
            }

            /// <summary>
            /// The cheapest place for one piece on this sheet. Every corner of every block is
            /// tried, because a piece pushed into the far corner leaves one wide offcut where
            /// the near corner would have left two unusable strips.
            /// </summary>
            public Spot? FindSpot(Piece piece)
            {
                Spot? best = null;

                foreach (var spot in Corners(piece))
                    if (best == null || spot.BetterThan(best)) best = spot;

                return best;
            }

            public void Place(Piece piece, Spot spot) =>
                PlaceAt(piece, _free[spot.FreeIndex], spot.FreeIndex, spot.X, spot.Y, spot.W, spot.H, spot.Rotated);

            private void PlaceAt(Piece piece, Rect free, int freeIndex, int x, int y, int w, int h, bool rotated)
            {
                _free.RemoveAt(freeIndex);
                foreach (var left in Spot.Split(free, x, y, w, h))
                    if (left.Area > 0) _free.Add(left);

                MergeFreeRects();

                Layout.Placements.Add(new CutPlacement
                {
                    SerialNumber  = piece.Serial,
                    X             = x,
                    Y             = y,
                    WidthSixteenths  = w,
                    HeightSixteenths = h,
                    Rotated       = rotated
                });
                Placed.Add(new PlacedPiece(piece, free, freeIndex, x, y, w, h, rotated));
            }

            /// <summary>
            /// Sawing a block leaves strips that lie next to each other, and two strips that
            /// meet flush along a full edge are one wider panel. Without this merge, the band
            /// under a pair of side-by-side panels stays invisible as two half-width offcuts
            /// and the packer buys a sheet it does not need.
            /// </summary>
            private void MergeFreeRects()
            {
                bool merged;
                do
                {
                    merged = false;

                    for (int i = 0; i < _free.Count && !merged; i++)
                        for (int j = i + 1; j < _free.Count; j++)
                        {
                            Rect? joined = JoinIfFlush(_free[i], _free[j]);
                            if (joined == null) continue;

                            _free[i] = joined.Value;
                            _free.RemoveAt(j);
                            merged = true;
                            break;
                        }
                }
                while (merged);
            }

            /// <summary>
            /// The union of two blocks that share a whole edge, or null if they do not line up.
            /// Flush and touching means the union covers no extra area, so it can never reach
            /// over a piece that has already been sawn out.
            /// </summary>
            private static Rect? JoinIfFlush(Rect a, Rect b)
            {
                if (a.Y == b.Y && a.H == b.H)
                {
                    if (a.Right == b.X) return new Rect(a.X, a.Y, a.W + b.W, a.H);
                    if (b.Right == a.X) return new Rect(b.X, a.Y, a.W + b.W, a.H);
                }

                if (a.X == b.X && a.W == b.W)
                {
                    if (a.Bottom == b.Y) return new Rect(a.X, a.Y, a.W, a.H + b.H);
                    if (b.Bottom == a.Y) return new Rect(b.X, a.Y, a.W, a.H + b.H);
                }

                return null;
            }
        }

        /// <summary>
        /// One partial plan inside the beam. Adding a piece copies only the sheet it lands on,
        /// so plans that grew the same way keep sharing those sheets.
        /// </summary>
        private sealed class State
        {
            public List<OpenSheet> Sheets { get; }

            /// <summary>Scrap on every sheet so far that no future bill can cut from.</summary>
            public int Dead { get; }

            public State(List<OpenSheet> sheets, int dead)
            {
                Sheets = sheets;
                Dead   = dead;
            }

            public int LastSheetUsed => Sheets.Count == 0 ? 0 : Sheets[^1].Layout.UsedArea;

            public string Signature => Sheets.Count + ": " + string.Join(" / ", Sheets.Select(s => s.Signature));

            public State PlaceOn(OpenSheet target, Spot spot, Piece piece, int widthSixteenths, int heightSixteenths)
            {
                var sheets = new List<OpenSheet>(Sheets);
                var copy   =  OpenSheet.Rebuild(target, widthSixteenths, heightSixteenths);
                copy.Place(piece, spot);
                sheets[sheets.IndexOf(target)] = copy;

                return new State(sheets, sheets.Sum(s => s.DeadArea));
            }

            public State StartNewSheet(Piece piece, int widthSixteenths, int heightSixteenths)
            {
                var fresh = new OpenSheet(Sheets.Count + 1, widthSixteenths, heightSixteenths);
                fresh.Place(piece, fresh.FindSpot(piece)!);

                var sheets = new List<OpenSheet>(Sheets) { fresh };
                return new State(sheets, Dead + fresh.DeadArea);
            }

            public Plan ToPlan(List<Piece> unplaced)
            {
                var sheets = Sheets.ToList();
                for (int i = 0; i < sheets.Count; i++) sheets[i].Layout.Number = i + 1;
                return new Plan(sheets, unplaced);
            }
        }

        private sealed class Plan
        {
            public List<OpenSheet> Sheets { get; }

            /// <summary>Pieces bigger than any sheet, in any direction.</summary>
            public List<Piece> Unplaced { get; }

            /// <summary>Pieces that would fit a sheet, with none of the entered stock left to cut.</summary>
            public List<Piece> Starved { get; }

            public Plan(List<OpenSheet> sheets, List<Piece> unplaced, List<Piece>? starved = null)
            {
                Sheets   = sheets;
                Unplaced = unplaced;
                Starved  = starved ?? new List<Piece>();
            }

            public int LastSheetUsed => Sheets.Count == 0 ? 0 : Sheets[^1].Layout.UsedArea;
            public int DeadArea      => Sheets.Sum(s => s.DeadArea);
        }
    }
}
