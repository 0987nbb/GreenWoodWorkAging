using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The cutting plan for one bill: how many sheets are needed, which sizes come off
    /// each sheet, where they sit and what is left over as scrap.
    /// </summary>
    public sealed class SheetOptimizerForm : Form
    {
        private static readonly Color CBg     = Color.FromArgb(242, 245, 249);
        private static readonly Color CCard   = Color.White;
        private static readonly Color CBorder = Color.FromArgb(218, 226, 234);
        private static readonly Color CText   = Color.FromArgb(24, 30, 42);
        private static readonly Color CSub    = Color.FromArgb(130, 142, 158);
        private static readonly Color CGreen  = Color.FromArgb(34, 139, 87);
        private static readonly Color CBlue   = Color.FromArgb(37, 99, 180);
        private static readonly Color CRed    = Color.FromArgb(203, 40, 40);

        private static readonly Font FTitle  = new("Segoe UI", 15f, FontStyle.Bold);
        private static readonly Font FLine   = new("Segoe UI", 10f);
        private static readonly Font FSheet  = new("Segoe UI", 10.5f, FontStyle.Bold);
        private static readonly Font FMeta   = new("Segoe UI", 8.5f);
        private static readonly Font FLegend = new("Consolas", 9f);
        private static readonly Font FBtn    = new("Segoe UI", 9.5f, FontStyle.Bold);

        // A standard sheet is 48 × 96 inches, so it fills the box below exactly. A bill cut from
        // other plywood keeps its own shape inside the same box instead of being stretched into
        // an 8 ft × 4 ft picture it is not.
        private const int DiagramW = 248;
        private const int DiagramH = 496;
        private const int LegendW  = 186;

        private readonly MeasurementDisplayFormatter _fmt = new();
        private readonly OptimizationResult _plan;
        private readonly string? _customer;

        public SheetOptimizerForm(OptimizationResult plan, string? customer)
        {
            _plan     = plan;
            _customer = customer;

            Text          = string.IsNullOrWhiteSpace(customer) ? "Sheet cutting plan"
                                                                : $"Sheet cutting plan — {customer}";
            BackColor     = CBg;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize    = new Size(1180, 800);
            MinimumSize   = new Size(720, 520);
            Font          = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = CBg,
                Padding     = new Padding(18, 14, 18, 14)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var header = BuildHeader(plan);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(BuildSheets(plan), 0, 1);

            Controls.Add(root);
        }

        // ── Header ───────────────────────────────────────────────────────────

        private Control BuildHeader(OptimizationResult plan)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(16, 10, 16, 10) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            string titleText = "⛁  Sheet cutting plan";

            // One sheet size is named in the headline; a plan cut from several is named sheet by
            // sheet, because "of 96 × 48 in" over a square PVC card would be a lie.
            string howMany = $"{plan.Sheets.Count} sheet{(plan.Sheets.Count == 1 ? "" : "s")}";
            bool mixedSizes = _plan.Sheets
                                   .Select(s => (s.SheetWidthSixteenths, s.SheetHeightSixteenths))
                                   .Distinct().Count() > 1;
            string sheetsPhrase = mixedSizes
                ? howMany + ", in the sizes entered"
                : howMany + " of " + SheetSizeText(plan.SheetWidthSixteenths, plan.SheetHeightSixteenths);

            // Every line the header shows, in order, with whether it is an error note. The card is
            // then exactly as tall as the lines it holds — a plan cut from counted stock, or one
            // that ran out of it, carries lines a plain plan never had.
            var notes = new List<(string Text, bool IsWarning)>
            {
                ((plan.Sheets.Count == 0
                    ? "Nothing to cut — add the sizes on the bill first."
                    : $"{sheetsPhrase}   ·   " +
                      $"{plan.PieceCount} pieces   ·   {plan.OverallUsedPercent:0.#}% of the plywood used" +
                      "   ·   dashed = unused"), false)
            };

            // When the bill named its sheets, say how the plan is split between them.
            var bySource = plan.Sheets
                               .GroupBy(s => s.Source)
                               .Where(g => g.Key.Length > 0)
                               .ToList();
            if (bySource.Count > 1)
                notes.Add((string.Join("   ·   ", bySource.Select(g =>
                    $"{g.Key}: {g.Count()} sheet{(g.Count() == 1 ? "" : "s")} (no. {g.Min(s => s.Number)}–{g.Max(s => s.Number)})")), false));

            if (plan.Stock.Count > 0)
                notes.Add((StockUsageText(plan), false));

            // The pieces missing from this plan are missing in two different ways, and the shop
            // fixes them differently: a bigger sheet, or more stock.
            var tooBig = plan.Unplaced.Where(u => u.Reason != UnplacedReason.OutOfStock).ToList();
            if (tooBig.Count > 0)
                notes.Add((WarningText("are bigger than a sheet and cannot be cut", null, tooBig), true));

            var shortUp = plan.Shortfalls;
            if (shortUp.Count > 0)
                notes.Add((WarningText("would fit but the stock entered ran out",
                                       "Enter more sheets and optimise again.", shortUp), true));

            int titleH = Math.Max(30, Measure(titleText, FTitle).Height + 6);
            int lineH  = Math.Max(22, Measure("0", FLine).Height + 6);
            int metaH  = Math.Max(20, Measure("0", FMeta).Height + 6);

            int y = 10;
            var title = new Label
            {
                Text      = titleText,
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = true,
                Location  = new Point(14, y)
            };
            y += titleH + 4;

            foreach (var (text, isWarning) in notes)
            {
                card.Controls.Add(new Label
                {
                    Text        = text,
                    Font        = isWarning ? FMeta : FLine,
                    ForeColor   = isWarning ? CRed : plan.Sheets.Count == 0 ? CSub : CGreen,
                    AutoSize    = true,
                    MaximumSize = new Size(980, 0),
                    Location    = new Point(16, y)
                });
                y += (isWarning ? metaH : lineH) * WrappedLines(text, isWarning ? FMeta : FLine) + 2;
            }

            var savePdf = BuildButton("⬇  Save PDF", CGreen);
            var back    = BuildButton("←  Back",     CBlue);
            savePdf.Click += (_, _) => SavePlanAsPdf();
            back.Click    += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            // Right-anchored as a pair, so they keep their order when the window is widened.
            card.Resize += (_, _) =>
            {
                back.Location    = new Point(card.ClientSize.Width  - back.Width - 14, 12);
                savePdf.Location = new Point(back.Left - savePdf.Width - 8, 12);
            };

            card.Controls.Add(title);
            card.Controls.Add(savePdf);
            card.Controls.Add(back);

            card.Height = y + 12;
            return card;
        }

        /// <summary>One missing-size note: why the pieces are off the plan and what to do about it.</summary>
        private string WarningText(string why, string? fix, List<UnplacedPiece> pieces) =>
            $"⚠  {pieces.Count} piece(s) {why}:  " +
            string.Join(", ", pieces.Select(u => $"{SizeText(u)} (row {u.SerialNumber})")) +
            (fix == null ? "" : ".  " + fix);

        /// <summary>How many lines this text takes once the card's width is reached.</summary>
        private static int WrappedLines(string text, Font font) =>
            Math.Max(1, (int)Math.Ceiling(Measure(text, font).Width / 960.0));

        /// <summary>
        /// The sheet the plan is cut from, in inches, named the way the shop names it —
        /// "96 × 48 in (8 ft × 4 ft)" for plywood, "72 × 72 in (6 ft × 6 ft)" for anything else.
        /// </summary>
        private string SheetSizeText(int widthSixteenths, int heightSixteenths)
        {
            double w = widthSixteenths  / (double)Sooter.PerInch;
            double h = heightSixteenths / (double)Sooter.PerInch;

            string size = $"{h:0.#} × {w:0.#} in";
            if (w % 1 == 0 && h % 1 == 0)
                size += $"  ({h / 12:0.#} ft × {w / 12:0.#} ft)";
            return size;
        }

        /// <summary>
        /// What the entered stock was used for: how many of each counted pile the plan opens, so
        /// the shop sees the sheets it has to buy before the carpenter asks for them.
        /// </summary>
        private string StockUsageText(OptimizationResult plan)
        {
            var parts = plan.Stock
                            .Where(s => s.Used > 0 || s.ShortPieces > 0)
                            .Select(s =>
                            {
                                string size = $"{s.HeightInches:0.#} × {s.WidthInches:0.#} in";
                                string name = string.IsNullOrWhiteSpace(s.Name) ? size : $"{s.Name} ({size})";
                                string count = s.Unlimited
                                    ? $"{s.Used} sheet{(s.Used == 1 ? "" : "s")}"
                                    : $"{s.Used} of {s.Available} used";
                                return $"{name}: {count}";
                            });

            string line = string.Join("   ·   ", parts);
            return line.Length == 0 ? "Stock: none of the sheets entered was needed."
                                    : "Stock  ·  " + line;
        }

        private string SizeText(UnplacedPiece u) =>
            $"{_fmt.FormatCompactFromSixteenths(u.HeightSixteenths)} × " +
            $"{_fmt.FormatCompactFromSixteenths(u.WidthSixteenths)}";

        private Button BuildButton(string text, Color color)
        {
            var button = new Button
            {
                Text      = text,
                AutoSize  = true,
                Padding   = new Padding(10, 4, 10, 4),
                Font      = FBtn,
                BackColor = Color.White,
                ForeColor = color,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = color;
            button.FlatAppearance.BorderSize  = 1;
            button.MouseEnter += (_, _) => { button.BackColor = color; button.ForeColor = Color.White; };
            button.MouseLeave += (_, _) => { button.BackColor = Color.White; button.ForeColor = color; };
            return button;
        }

        // ── Sheets ────────────────────────────────────────────────────────────

        private Control BuildSheets(OptimizationResult plan)
        {
            var flow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                AutoScroll    = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = true,
                Padding       = new Padding(2, 8, 2, 8),
                BackColor     = CBg
            };

            foreach (var sheet in plan.Sheets)
                flow.Controls.Add(BuildSheetCard(sheet));

            return flow;
        }

        private Control BuildSheetCard(SheetLayout sheet)
        {
            (int diagramW, int diagramH) = DiagramBox(sheet);
            int cardW = diagramW + LegendW + 40;

            string headingText = $"Sheet {sheet.Number}" +
                                 (string.IsNullOrWhiteSpace(sheet.Source) ? "" : $"  ·  {sheet.Source}") +
                                 (string.IsNullOrWhiteSpace(sheet.StockName) ||
                                  string.Equals(sheet.StockName, sheet.Source, StringComparison.OrdinalIgnoreCase)
                                     ? "" : $"  ·  {sheet.StockName}");

            // A plan cut from several kinds of sheet says so on every card, because the size the
            // carpenter measures against is the one on the pile in front of them.
            string sizeText = DiagramsDiffer(sheet)
                ? SheetSizeText(sheet.SheetWidthSixteenths, sheet.SheetHeightSixteenths) + "   ·   "
                : "";
            string usedText = $"{sizeText}{sheet.Placements.Count} pieces · {sheet.UsedPercent:0.#}% used";

            // Both halves of the heading are measured. A fixed 18 px row shaves the bottom off
            // "Sheet 1", and a label allowed only half the card hides the material code the
            // plan was grouped by — which is the one thing the shop needs to see here.
            int headingH = Math.Max(24, Measure(headingText, FSheet).Height + 6);
            int usedW    = Math.Min(cardW - 60, Measure(usedText, FMeta).Width + 8);
            int cardH    = diagramH + headingH + 20;

            var card = new Panel { Size = new Size(cardW, cardH), Margin = new Padding(10), BackColor = CCard };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            var heading = new Label
            {
                Text         = headingText,
                Font         = FSheet,
                ForeColor    = CText,
                AutoEllipsis = true,
                Size         = new Size(cardW - 24 - usedW - 10, headingH),
                Location     = new Point(12, 6),
                TextAlign    = ContentAlignment.BottomLeft
            };

            var used = new Label
            {
                Text      = usedText,
                Font      = FMeta,
                ForeColor = sheet.UsedPercent >= 80 ? CGreen : CSub,
                TextAlign = ContentAlignment.BottomRight,
                Size      = new Size(usedW, headingH),
                Location  = new Point(cardW - 12 - usedW, 6)
            };

            int diagramTop = 6 + headingH + 8;

            var diagram = new SheetDiagram(sheet)
            {
                Location = new Point(12, diagramTop),
                Size     = new Size(diagramW, diagramH)
            };

            var legend = new Label
            {
                Text          = LegendFor(sheet),
                Font          = FLegend,
                ForeColor     = CText,
                AutoSize      = false,
                Size          = new Size(LegendW, diagramH),
                Location      = new Point(12 + diagramW + 16, diagramTop + 2)
            };

            card.Controls.Add(heading);
            card.Controls.Add(used);
            card.Controls.Add(diagram);
            card.Controls.Add(legend);
            return card;
        }

        /// <summary>
        /// The picture box for one sheet: the standard 48 × 96 sheet fills it, and any other
        /// sheet keeps its own shape inside the same limits instead of being drawn as plywood
        /// it is not.
        /// </summary>
        private static (int Width, int Height) DiagramBox(SheetLayout sheet)
        {
            if (sheet.SheetWidthSixteenths <= 0 || sheet.SheetHeightSixteenths <= 0)
                return (DiagramW, DiagramH);

            double scale = Math.Min(DiagramW / (double)sheet.SheetWidthSixteenths,
                                    DiagramH / (double)sheet.SheetHeightSixteenths);

            return (Math.Max(120, (int)Math.Round(sheet.SheetWidthSixteenths  * scale)),
                    Math.Max(120, (int)Math.Round(sheet.SheetHeightSixteenths * scale)));
        }

        /// <summary>True when the plan mixes sheet sizes, so each card has to name its own.</summary>
        private bool DiagramsDiffer(SheetLayout sheet) =>
            _plan.Sheets.Any(s => s.SheetWidthSixteenths != sheet.SheetWidthSixteenths ||
                                   s.SheetHeightSixteenths != sheet.SheetHeightSixteenths);

        /// <summary>How wide a line of this text really is, so nothing is cut off by a fixed box.</summary>
        private static Size Measure(string text, Font font) =>
            TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

        /// <summary>
        /// Every size on the sheet, listed beside the picture. A 3-inch strip of plywood is a
        /// handful of pixels wide, so its number cannot always be written on it — this is where
        /// those sizes are still read.
        /// </summary>
        private string LegendFor(SheetLayout sheet)
        {
            var lines = sheet.Placements
                             .GroupBy(p => new
                             {
                                 p.SerialNumber,
                                 TypedHeight = p.Rotated ? p.WidthSixteenths : p.HeightSixteenths,
                                 TypedWidth  = p.Rotated ? p.HeightSixteenths : p.WidthSixteenths
                             })
                             .OrderBy(g => g.Key.SerialNumber)
                             .Select(g =>
                             {
                                 string size = $"{_fmt.FormatCompactFromSixteenths(g.Key.TypedHeight)} × " +
                                               $"{_fmt.FormatCompactFromSixteenths(g.Key.TypedWidth)}";
                                 return $"#{g.Key.SerialNumber,-3} {size,-11} × {g.Count()}";
                             });

            return string.Join("\n", lines);
        }

        // ── Saving ────────────────────────────────────────────────────────────

        private void SavePlanAsPdf()
        {
            string name = _customer?.Trim() ?? "";
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GreenWood Bills");
            Directory.CreateDirectory(folder);

            using var dlg = new SaveFileDialog
            {
                Title            = "Save cutting plan as PDF",
                Filter           = "PDF file (*.pdf)|*.pdf",
                FileName         = SafeFileName($"Cutting-Plan-{(name.Length == 0 ? "" : name + "-")}{DateTime.Today:yyyyMMdd}"),
                InitialDirectory = folder
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                SheetPlanPdfService.Generate(dlg.FileName, _plan, name, DateTime.Today);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create the PDF: " + ex.Message, "Cutting plan",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>A file name a customer typed cannot break the file system or confuse Explorer.</summary>
        private static string SafeFileName(string raw)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
                raw = raw.Replace(bad, '-');
            raw = raw.Replace(' ', '-').Trim('-');
            return raw.Length == 0 ? "Cutting-Plan" : (raw.Length > 60 ? raw[..60] : raw);
        }

        private static void PaintBorder(Graphics g, Control c)
        {
            using var pen = new Pen(CBorder, 1f);
            g.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }

        /// <summary>One sheet, drawn by the same renderer a printed plan would use.</summary>
        private sealed class SheetDiagram : Panel
        {
            private readonly SheetLayout _sheet;

            public SheetDiagram(SheetLayout sheet)
            {
                _sheet    = sheet;
                BackColor = Color.White;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e) =>
                SheetDiagramRenderer.Draw(e.Graphics, ClientRectangle, _sheet);
        }
    }
}
