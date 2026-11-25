using GreenWoodWorkAging.Models;
using GreenWoodWorkAging.Services;

namespace GreenWoodWorkAging
{
    /// <summary>
    /// The cutting plan for one bill: how many sheets are needed, which sizes come off
    /// each sheet, where they sit and what is left over as scrap.
    ///
    /// Features:
    ///  • Blade thickness shown in the header.
    ///  • Interactive drag-and-drop layout: pieces can be moved between sheets.
    ///  • Validation: a dragged piece cannot land outside sheet boundaries, cannot overlap
    ///    another piece, and must respect the blade-kerf gap from neighbours.
    ///  • After every move the layout and legend refresh, and the PDF export uses the
    ///    manually-adjusted plan.
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
        private static readonly Color CAmber  = Color.FromArgb(180, 100, 10);

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

        // Scrollable area that holds all the sheet cards — rebuilt on each layout change.
        private FlowLayoutPanel _sheetsPanel = null!;
        // Status bar label at the bottom of the window.
        private Label _statusLabel = null!;
        private readonly System.Windows.Forms.Timer _dragAutoScrollTimer = new() { Interval = 40 };

        public SheetOptimizerForm(OptimizationResult plan, string? customer)
        {
            _plan     = plan;
            _customer = customer;
            _dragAutoScrollTimer.Tick += OnDragAutoScrollTick;

            Text          = string.IsNullOrWhiteSpace(customer) ? "Sheet cutting plan"
                                                                : $"Sheet cutting plan — {customer}";
            BackColor     = CBg;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize    = new Size(1240, 840);
            MinimumSize   = new Size(780, 540);
            Font          = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = CBg,
                Padding     = new Padding(18, 14, 18, 14)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var header = BuildHeader(plan);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, header.Height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            _statusLabel = new Label
            {
                Dock      = DockStyle.Fill,
                Font      = FMeta,
                ForeColor = CSub,
                TextAlign = ContentAlignment.MiddleLeft,
                Text      = "Drag pieces between sheets · Press 'R', Space, or Right-Click to rotate (Horizontal ↔ Vertical) · PDF export uses this layout"
            };

            // Build the diagrams only after the status label exists: each diagram keeps a
            // reference to it for drag and rotation feedback.
            _sheetsPanel = BuildSheetsPanel();

            root.Controls.Add(header,        0, 0);
            root.Controls.Add(_sheetsPanel,  0, 1);
            root.Controls.Add(_statusLabel,  0, 2);

            Controls.Add(root);
        }

        // ── Header ───────────────────────────────────────────────────────────

        private Control BuildHeader(OptimizationResult plan)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = CCard, Padding = new Padding(16, 10, 16, 10) };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            string titleText = "⛁  Sheet cutting plan";

            string howMany = $"{plan.Sheets.Count} sheet{(plan.Sheets.Count == 1 ? "" : "s")}";
            bool mixedSizes = _plan.Sheets
                                   .Select(s => (s.SheetWidthSixteenths, s.SheetHeightSixteenths))
                                   .Distinct().Count() > 1;
            string sheetsPhrase = mixedSizes
                ? howMany + ", in the sizes entered"
                : howMany + " of " + SheetSizeText(plan.SheetWidthSixteenths, plan.SheetHeightSixteenths);

            var notes = new List<(string Text, bool IsWarning)>
            {
                ((plan.Sheets.Count == 0
                    ? "Nothing to cut — add the sizes on the bill first."
                    : $"{sheetsPhrase}   ·   " +
                      $"{plan.PieceCount} pieces   ·   {plan.OverallUsedPercent:0.#}% of the plywood used" +
                      "   ·   dashed = unused"), false)
            };

            // Blade thickness line
            if (plan.BladeThicknessSixteenths > 0)
                notes.Add(($"✂  Blade thickness: {plan.BladeThicknessSooter:0.##} Suter " +
                           $"({plan.BladeThicknessSixteenths / (double)Sooter.PerInch:0.####} inch) — kerf included in layout", false));

            var bySource = plan.Sheets
                               .GroupBy(s => s.Source)
                               .Where(g => g.Key.Length > 0)
                               .ToList();
            if (bySource.Count > 1)
                notes.Add((string.Join("   ·   ", bySource.Select(g =>
                    $"{g.Key}: {g.Count()} sheet{(g.Count() == 1 ? "" : "s")} (no. {g.Min(s => s.Number)}–{g.Max(s => s.Number)})")), false));

            if (plan.Stock.Count > 0)
                notes.Add((StockUsageText(plan), false));

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
                    MaximumSize = new Size(1100, 0),
                    Location    = new Point(16, y)
                });
                y += (isWarning ? metaH : lineH) * WrappedLines(text, isWarning ? FMeta : FLine) + 2;
            }

            var savePdf = BuildButton("⬇  Save PDF", CGreen);
            var back    = BuildButton("←  Back",     CBlue);
            savePdf.Click += (_, _) => SavePlanAsPdf();
            back.Click    += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

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

        // ── Sheets ────────────────────────────────────────────────────────────

        private FlowLayoutPanel BuildSheetsPanel()
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

            foreach (var sheet in _plan.Sheets)
                flow.Controls.Add(BuildSheetCard(sheet));

            return flow;
        }

        /// <summary>
        /// Rebuilds the scrollable sheet area after a drag-and-drop move,
        /// keeping the scroll position so the user does not lose their place.
        /// </summary>
        private void RefreshSheetsPanel()
        {
            var parent = _sheetsPanel.Parent;
            if (parent == null) return;

            var scroll = _sheetsPanel.AutoScrollPosition;
            var newPanel = BuildSheetsPanel();
            newPanel.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);

            int idx = parent.Controls.IndexOf(_sheetsPanel);
            parent.Controls.Remove(_sheetsPanel);
            _sheetsPanel.Dispose();
            _sheetsPanel = newPanel;
            parent.Controls.Add(_sheetsPanel);
            if (parent is TableLayoutPanel tlp)
                tlp.SetCellPosition(_sheetsPanel, new TableLayoutPanelCellPosition(0, 1));
        }

        private void StartDragAutoScroll() => _dragAutoScrollTimer.Start();

        private void StopDragAutoScroll() => _dragAutoScrollTimer.Stop();

        private void OnDragAutoScrollTick(object? sender, EventArgs e)
        {
            if (IsDisposed || _sheetsPanel.IsDisposed || !_sheetsPanel.IsHandleCreated) return;

            Point cursor = _sheetsPanel.PointToClient(Control.MousePosition);
            if (cursor.X < 0 || cursor.X >= _sheetsPanel.ClientSize.Width) return;

            const int edgeSize = 48;
            const int scrollStep = 16;
            int currentOffset = -_sheetsPanel.AutoScrollPosition.Y;

            if (cursor.Y < edgeSize)
                _sheetsPanel.AutoScrollPosition = new Point(-_sheetsPanel.AutoScrollPosition.X,
                                                             Math.Max(0, currentOffset - scrollStep));
            else if (cursor.Y >= _sheetsPanel.ClientSize.Height - edgeSize)
                _sheetsPanel.AutoScrollPosition = new Point(-_sheetsPanel.AutoScrollPosition.X,
                                                             currentOffset + scrollStep);
        }

        private Control BuildSheetCard(SheetLayout sheet)
        {
            (int diagramW, int diagramH) = DiagramBox(sheet);
            int cardW = diagramW + LegendW + 44;

            string headingText = $"Sheet {sheet.Number}" +
                                 (string.IsNullOrWhiteSpace(sheet.Source) ? "" : $"  ·  {sheet.Source}") +
                                 (string.IsNullOrWhiteSpace(sheet.StockName) ||
                                  string.Equals(sheet.StockName, sheet.Source, StringComparison.OrdinalIgnoreCase)
                                     ? "" : $"  ·  {sheet.StockName}");

            string sizeText = DiagramsDiffer(sheet)
                ? SheetSizeText(sheet.SheetWidthSixteenths, sheet.SheetHeightSixteenths) + "   ·   "
                : "";

            int usedPct = (int)Math.Round(sheet.UsedPercent);
            int scrapArea = sheet.Scrap.Sum(s => s.Area);
            double scrapPct = sheet.SheetArea > 0 ? scrapArea * 100.0 / sheet.SheetArea : 0;
            string usedText = $"{sizeText}{sheet.Placements.Count} pieces · {sheet.UsedPercent:0.#}% used · {scrapPct:0.#}% waste";

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

            var diagram = new SheetDiagram(sheet, _plan, _fmt, _statusLabel, this)
            {
                Location = new Point(12, diagramTop),
                Size     = new Size(diagramW, diagramH)
            };
            diagram.PieceMoved += () => RefreshSheetsPanel();

            // Legend panel — a styled Panel with owner-draw legend lines for colour swatches
            var legend = BuildLegendPanel(sheet, diagramH, new Point(12 + diagramW + 16, diagramTop + 2));

            card.Controls.Add(heading);
            card.Controls.Add(used);
            card.Controls.Add(diagram);
            card.Controls.Add(legend);
            return card;
        }

        // ── Legend ────────────────────────────────────────────────────────────

        private Panel BuildLegendPanel(SheetLayout sheet, int height, Point location)
        {
            var panel = new Panel
            {
                Size     = new Size(LegendW, height),
                Location = location,
                BackColor = CCard
            };

            var groups = sheet.Placements
                              .GroupBy(p => new
                              {
                                  p.SerialNumber,
                                  TypedH = p.Rotated ? p.WidthSixteenths : p.HeightSixteenths,
                                  TypedW = p.Rotated ? p.HeightSixteenths : p.WidthSixteenths
                              })
                              .OrderBy(g => g.Key.SerialNumber)
                              .ToList();

            int rowH = 22;
            int y    = 4;

            // Header row
            var hdr = new Label
            {
                Text      = "#    Size                 Qty",
                Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = CSub,
                AutoSize  = false,
                Size      = new Size(LegendW - 4, 18),
                Location  = new Point(2, y),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(hdr);
            y += 20;

            foreach (var g in groups)
            {
                var row = new Panel
                {
                    Location  = new Point(0, y),
                    Size      = new Size(LegendW, rowH),
                    BackColor = CCard
                };

                // Colour swatch
                var swatch = new Panel
                {
                    Location  = new Point(2, 4),
                    Size      = new Size(10, 14),
                    BackColor = SheetDiagramRenderer.ColorFor(g.Key.SerialNumber),
                    BorderStyle = BorderStyle.FixedSingle
                };

                string sizeStr = $"{_fmt.FormatCompactFromSixteenths(g.Key.TypedH)} × " +
                                 $"{_fmt.FormatCompactFromSixteenths(g.Key.TypedW)}";
                string lineText = $"#{g.Key.SerialNumber,-3} {sizeStr}";

                var rowLbl = new Label
                {
                    Text      = lineText,
                    Font      = FLegend,
                    ForeColor = CText,
                    AutoSize  = false,
                    Size      = new Size(LegendW - 44, rowH),
                    Location  = new Point(16, 2),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var qtyLbl = new Label
                {
                    Text      = $"×{g.Count()}",
                    Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = CGreen,
                    AutoSize  = false,
                    Size      = new Size(28, rowH),
                    Location  = new Point(LegendW - 32, 2),
                    TextAlign = ContentAlignment.MiddleRight
                };

                row.Controls.Add(swatch);
                row.Controls.Add(rowLbl);
                row.Controls.Add(qtyLbl);
                panel.Controls.Add(row);
                y += rowH;
            }

            // Waste line
            if (y + 20 < height)
            {
                int scrapArea = sheet.Scrap.Sum(s => s.Area);
                double scrapPct = sheet.SheetArea > 0 ? scrapArea * 100.0 / sheet.SheetArea : 0;
                var wasteLbl = new Label
                {
                    Text      = $"⬜  Waste: {scrapPct:0.#}%",
                    Font      = FMeta,
                    ForeColor = scrapPct < 20 ? CGreen : scrapPct < 40 ? CAmber : CRed,
                    AutoSize  = true,
                    Location  = new Point(2, y + 4)
                };
                panel.Controls.Add(wasteLbl);
            }

            return panel;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string WarningText(string why, string? fix, List<UnplacedPiece> pieces) =>
            $"⚠  {pieces.Count} piece(s) {why}:  " +
            string.Join(", ", pieces.Select(u => $"{SizeText(u)} (row {u.SerialNumber})")) +
            (fix == null ? "" : ".  " + fix);

        private static int WrappedLines(string text, Font font) =>
            Math.Max(1, (int)Math.Ceiling(Measure(text, font).Width / 1060.0));

        private string SheetSizeText(int widthSixteenths, int heightSixteenths)
        {
            double w = widthSixteenths  / (double)Sooter.PerInch;
            double h = heightSixteenths / (double)Sooter.PerInch;

            string size = $"{h:0.#} × {w:0.#} in";
            if (w % 1 == 0 && h % 1 == 0)
                size += $"  ({h / 12:0.#} ft × {w / 12:0.#} ft)";
            return size;
        }

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

        private static (int Width, int Height) DiagramBox(SheetLayout sheet)
        {
            if (sheet.SheetWidthSixteenths <= 0 || sheet.SheetHeightSixteenths <= 0)
                return (DiagramW, DiagramH);

            double scale = Math.Min(DiagramW / (double)sheet.SheetWidthSixteenths,
                                    DiagramH / (double)sheet.SheetHeightSixteenths);

            return (Math.Max(120, (int)Math.Round(sheet.SheetWidthSixteenths  * scale)),
                    Math.Max(120, (int)Math.Round(sheet.SheetHeightSixteenths * scale)));
        }

        private bool DiagramsDiffer(SheetLayout sheet) =>
            _plan.Sheets.Any(s => s.SheetWidthSixteenths != sheet.SheetWidthSixteenths ||
                                   s.SheetHeightSixteenths != sheet.SheetHeightSixteenths);

        private static Size Measure(string text, Font font) =>
            TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

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

        // ── Drag-and-drop diagram ─────────────────────────────────────────────

        /// <summary>
        /// An interactive sheet diagram. The user can drag pieces to new positions or to other
        /// sheets, and rotate pieces (horizontal ↔ vertical) either by right-clicking in place
        /// or by pressing 'R', Space, or Right-Click during drag-and-drop.
        /// Validation prevents overlaps, kerf violations, and out-of-bounds placements.
        /// </summary>
        private sealed class SheetDiagram : Panel
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern short GetAsyncKeyState(int vKey);

            // Static drag state shared across all diagrams of the same form.
            private static CutPlacement? _dragPiece;
            private static SheetLayout?  _dragSource;
            private static Point         _dragStartScreen;
            private static Point         _dragOffsetLogical; // offset in sheet-logical sixteenths

            // Rotation tracking during drag
            private static bool _dragRotated;
            private static bool _rKeyWasDown;
            private static bool _rightWasDown;
            private static int  _origX, _origY, _origW, _origH;
            private static bool _origRotated;

            private readonly SheetLayout         _sheet;
            private readonly OptimizationResult  _plan;
            private readonly MeasurementDisplayFormatter _fmt;
            private readonly Label               _status;
            private readonly SheetOptimizerForm  _owner;

            // Ghost rect drawn while dragging
            private Rectangle _ghostRect = Rectangle.Empty;
            private bool      _ghostValid = false;

            public event Action? PieceMoved;

            public SheetDiagram(SheetLayout sheet, OptimizationResult plan,
                                MeasurementDisplayFormatter fmt, Label status, SheetOptimizerForm owner)
            {
                _sheet  = sheet;
                _plan   = plan;
                _fmt    = fmt;
                _status = status;
                _owner  = owner;

                BackColor = Color.White;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

                // Allow incoming drops
                AllowDrop = true;

                MouseDown   += OnMouseDown;
                MouseMove   += OnMouseMove;
                MouseUp     += OnMouseUp;
                DragEnter   += OnDragEnter;
                DragOver    += OnDragOver;
                DragDrop    += OnDragDrop;
                DragLeave   += OnDragLeave;
                MouseWheel  += OnDiagramMouseWheel;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                SheetDiagramRenderer.Draw(e.Graphics, ClientRectangle, _sheet);

                // Draw the ghost rectangle while dragging over this sheet
                if (_ghostRect != Rectangle.Empty)
                {
                    Color ghostColor = _ghostValid
                        ? Color.FromArgb(100, 34, 139, 87)   // green tint = OK
                        : Color.FromArgb(100, 203, 40, 40);   // red tint = invalid

                    using var brush = new SolidBrush(ghostColor);
                    using var pen   = new Pen(_ghostValid ? Color.FromArgb(34, 139, 87) : Color.FromArgb(203, 40, 40), 2f);
                    e.Graphics.FillRectangle(brush, _ghostRect);
                    e.Graphics.DrawRectangle(pen,   _ghostRect);
                }
            }

            // ── Drag initiation & in-place rotate ──────────────────────────

            private void OnMouseDown(object? s, MouseEventArgs e)
            {
                var hit = HitTest(e.Location);
                if (hit == null) return;

                if (e.Button == MouseButtons.Right)
                {
                    // Right-click in place: toggle horizontal/vertical rotation
                    RotatePieceInPlace(hit);
                    return;
                }

                if (e.Button != MouseButtons.Left) return;

                _dragPiece  = hit;
                _dragSource = _sheet;
                _dragStartScreen = PointToScreen(e.Location);

                _origX       = hit.X;
                _origY       = hit.Y;
                _origW       = hit.WidthSixteenths;
                _origH       = hit.HeightSixteenths;
                _origRotated = hit.Rotated;

                _dragRotated  = false;
                _rKeyWasDown  = false;
                _rightWasDown = false;

                // Logical offset within the piece (in sixteenths) where the mouse clicked
                float scale = CalcScale();
                float offX = (e.X - hit.X * scale) / scale;
                float offY = (e.Y - hit.Y * scale) / scale;
                _dragOffsetLogical = new Point((int)offX, (int)offY);

                Cursor = Cursors.SizeAll;
            }

            private void RotatePieceInPlace(CutPlacement hit)
            {
                int newW = hit.HeightSixteenths;
                int newH = hit.WidthSixteenths;
                int kerf = _sheet.BladeThicknessSixteenths;

                var (snapX, snapY) = SheetValidationService.SnapPosition(
                    _sheet, hit.X, hit.Y, newW, newH, hit.Id, kerf);

                var candidate = new CutPlacement
                {
                    Id               = hit.Id,
                    SerialNumber     = hit.SerialNumber,
                    X                = snapX,
                    Y                = snapY,
                    WidthSixteenths  = newW,
                    HeightSixteenths = newH,
                    Rotated          = !hit.Rotated
                };

                var val = SheetValidationService.ValidatePlacement(_sheet, candidate, hit.Id, kerf);
                if (val.IsValid)
                {
                    hit.X                = snapX;
                    hit.Y                = snapY;
                    hit.WidthSixteenths  = newW;
                    hit.HeightSixteenths = newH;
                    hit.Rotated          = !hit.Rotated;
                    SheetValidationService.RecomputeScrap(_sheet, kerf);

                    ShowStatus($"✅ Piece #{hit.SerialNumber} rotated to {newW / (double)Sooter.PerInch:0.##}\" × {newH / (double)Sooter.PerInch:0.##}\" ({(hit.Rotated ? "Rotated" : "Normal")})", isError: false);
                    PieceMoved?.Invoke();
                }
                else
                {
                    ShowStatus($"⚠ Cannot rotate piece #{hit.SerialNumber} here ({val.Reason}) — drag to open space to place rotated.", isError: true);
                }
            }

            private void OnMouseMove(object? s, MouseEventArgs e)
            {
                if (_dragPiece == null || _dragSource != _sheet) return;
                if (e.Button != MouseButtons.Left) { ClearDrag(); return; }

                // Once the mouse has moved enough, start the OLE drag
                var curScreen = PointToScreen(e.Location);
                int dx = Math.Abs(curScreen.X - _dragStartScreen.X);
                int dy = Math.Abs(curScreen.Y - _dragStartScreen.Y);
                if (dx < 4 && dy < 4) return;

                // Keep scrolling while the native OLE drag loop owns mouse input. This lets
                // the user reach sheets that are currently outside the visible viewport.
                _owner.StartDragAutoScroll();
                try
                {
                    DoDragDrop(_dragPiece.Id, DragDropEffects.Move);
                }
                finally
                {
                    _owner.StopDragAutoScroll();
                    _ghostRect = Rectangle.Empty;
                    ClearDrag();
                    Invalidate();
                }
            }

            private void OnMouseUp(object? s, MouseEventArgs e)
            {
                ClearDrag();
                Cursor = Cursors.Default;
            }

            private void OnDiagramMouseWheel(object? s, MouseEventArgs e)
            {
                if (_dragPiece != null)
                {
                    _dragRotated = !_dragRotated;
                    var clientPt = PointToClient(Cursor.Position);
                    UpdateGhost(clientPt);
                }
            }

            // ── Drop target ────────────────────────────────────────────────

            private void OnDragEnter(object? s, DragEventArgs e)
            {
                if (e.Data?.GetData(DataFormats.Text) is string)
                    e.Effect = DragDropEffects.Move;
                else
                    e.Effect = DragDropEffects.None;
            }

            private void OnDragOver(object? s, DragEventArgs e)
            {
                if (_dragPiece == null) { e.Effect = DragDropEffects.None; return; }

                e.Effect = DragDropEffects.Move;

                // Check keyboard for 'R' key (0x52) or Space (0x20)
                bool rDown = (GetAsyncKeyState(0x52) & 0x8000) != 0 || (GetAsyncKeyState(0x20) & 0x8000) != 0;
                if (rDown && !_rKeyWasDown)
                {
                    _dragRotated = !_dragRotated;
                }
                _rKeyWasDown = rDown;

                // Check right mouse button during drag
                bool rightDown = (e.KeyState & 2) != 0;
                if (rightDown && !_rightWasDown)
                {
                    _dragRotated = !_dragRotated;
                }
                _rightWasDown = rightDown;

                // Work out ghost position in screen-to-client coordinates
                var clientPt = PointToClient(new Point(e.X, e.Y));
                UpdateGhost(clientPt);
            }

            private void OnDragLeave(object? s, EventArgs e)
            {
                _ghostRect = Rectangle.Empty;
                Invalidate();
            }

            private void OnDragDrop(object? s, DragEventArgs e)
            {
                _ghostRect = Rectangle.Empty;
                Invalidate();

                if (_dragPiece == null) return;

                var clientPt = PointToClient(new Point(e.X, e.Y));
                float scale  = CalcScale();
                float originX = (Width  - _sheet.SheetWidthSixteenths  * scale) / 2f;
                float originY = (Height - _sheet.SheetHeightSixteenths * scale) / 2f;

                int curW = _dragRotated ? _origH : _origW;
                int curH = _dragRotated ? _origW : _origH;
                int curOffX = _dragRotated ? _dragOffsetLogical.Y : _dragOffsetLogical.X;
                int curOffY = _dragRotated ? _dragOffsetLogical.X : _dragOffsetLogical.Y;

                // Target position in sixteenths
                int rawX = (int)Math.Round((clientPt.X - originX) / scale - curOffX);
                int rawY = (int)Math.Round((clientPt.Y - originY) / scale - curOffY);

                int kerf = _sheet.BladeThicknessSixteenths;
                var (snapX, snapY) = SheetValidationService.SnapPosition(
                    _sheet, rawX, rawY, curW, curH,
                    _dragPiece.Id, kerf);

                // Candidate placement for validation
                var candidate = new CutPlacement
                {
                    Id               = _dragPiece.Id,
                    SerialNumber     = _dragPiece.SerialNumber,
                    X                = snapX,
                    Y                = snapY,
                    WidthSixteenths  = curW,
                    HeightSixteenths = curH,
                    Rotated          = _dragRotated ? !_origRotated : _origRotated
                };

                var result = SheetValidationService.ValidatePlacement(
                    _sheet, candidate, _dragPiece.Id, kerf);

                if (!result.IsValid)
                {
                    ShowStatus($"❌ Cannot place here: {result.Reason}", isError: true);
                    // Restore original position and dimensions
                    _dragPiece.X                = _origX;
                    _dragPiece.Y                = _origY;
                    _dragPiece.WidthSixteenths  = _origW;
                    _dragPiece.HeightSixteenths = _origH;
                    _dragPiece.Rotated          = _origRotated;
                    ClearDrag();
                    return;
                }

                // Commit: update piece properties
                _dragPiece.X                = snapX;
                _dragPiece.Y                = snapY;
                _dragPiece.WidthSixteenths  = curW;
                _dragPiece.HeightSixteenths = curH;
                _dragPiece.Rotated          = _dragRotated ? !_origRotated : _origRotated;

                if (_dragSource != null && _dragSource != _sheet)
                {
                    _dragSource.Placements.Remove(_dragPiece);
                    SheetValidationService.RecomputeScrap(_dragSource, _dragSource.BladeThicknessSixteenths);

                    if (!_sheet.Placements.Any(p => p.Id == _dragPiece.Id))
                        _sheet.Placements.Add(_dragPiece);
                    SheetValidationService.RecomputeScrap(_sheet, _sheet.BladeThicknessSixteenths);
                }
                else
                {
                    // Same sheet — just moved to new position / orientation
                    SheetValidationService.RecomputeScrap(_sheet, _sheet.BladeThicknessSixteenths);
                }

                string rotText = _dragPiece.Rotated ? " (Rotated)" : "";
                ShowStatus($"✅ Piece #{_dragPiece.SerialNumber} ({curW / (double)Sooter.PerInch:0.##}\" × {curH / (double)Sooter.PerInch:0.##}\"{rotText}) placed on Sheet {_sheet.Number} at ({snapX / (double)Sooter.PerInch:0.##}\", {snapY / (double)Sooter.PerInch:0.##}\")", isError: false);

                ClearDrag();
                PieceMoved?.Invoke();
            }

            // ── Ghost / hit-test helpers ────────────────────────────────────

            private void UpdateGhost(Point clientPt)
            {
                if (_dragPiece == null) return;

                float scale   = CalcScale();
                float originX = (Width  - _sheet.SheetWidthSixteenths  * scale) / 2f;
                float originY = (Height - _sheet.SheetHeightSixteenths * scale) / 2f;

                int curW = _dragRotated ? _origH : _origW;
                int curH = _dragRotated ? _origW : _origH;
                int curOffX = _dragRotated ? _dragOffsetLogical.Y : _dragOffsetLogical.X;
                int curOffY = _dragRotated ? _dragOffsetLogical.X : _dragOffsetLogical.Y;

                int rawX = (int)Math.Round((clientPt.X - originX) / scale - curOffX);
                int rawY = (int)Math.Round((clientPt.Y - originY) / scale - curOffY);

                int kerf = _sheet.BladeThicknessSixteenths;
                var (snapX, snapY) = SheetValidationService.SnapPosition(
                    _sheet, rawX, rawY, curW, curH,
                    _dragPiece.Id, kerf);

                // Check validity for ghost colour
                var tmp = new CutPlacement
                {
                    SerialNumber     = _dragPiece.SerialNumber,
                    WidthSixteenths  = curW,
                    HeightSixteenths = curH,
                    X = snapX, Y = snapY,
                    Id = Guid.NewGuid().ToString("N")
                };
                _ghostValid = SheetValidationService.ValidatePlacement(_sheet, tmp, _dragPiece.Id, kerf).IsValid;

                _ghostRect = new Rectangle(
                    (int)(originX + snapX * scale),
                    (int)(originY + snapY * scale),
                    (int)(curW * scale),
                    (int)(curH * scale));

                string rotInfo = _dragRotated ? " [Rotated 90°]" : "";
                ShowStatus($"Piece #{_dragPiece.SerialNumber} ({curW / (double)Sooter.PerInch:0.##}\" × {curH / (double)Sooter.PerInch:0.##}\"{rotInfo}) — Press 'R', Space, or Right-Click to rotate", isError: !_ghostValid);

                Invalidate();
            }

            private CutPlacement? HitTest(Point clientPt)
            {
                float scale   = CalcScale();
                float originX = (Width  - _sheet.SheetWidthSixteenths  * scale) / 2f;
                float originY = (Height - _sheet.SheetHeightSixteenths * scale) / 2f;

                // Sixteenths coordinates of the click
                float lx = (clientPt.X - originX) / scale;
                float ly = (clientPt.Y - originY) / scale;

                return _sheet.Placements.LastOrDefault(p =>
                    lx >= p.X && lx < p.Right &&
                    ly >= p.Y && ly < p.Bottom);
            }

            private float CalcScale()
            {
                if (_sheet.SheetWidthSixteenths <= 0 || _sheet.SheetHeightSixteenths <= 0) return 1f;
                const int margin = 8;
                return Math.Min((Width  - margin * 2) / (float)_sheet.SheetWidthSixteenths,
                                (Height - margin * 2) / (float)_sheet.SheetHeightSixteenths);
            }

            private void ShowStatus(string msg, bool isError)
            {
                if (_status.IsHandleCreated)
                {
                    try
                    {
                        _status.BeginInvoke(() =>
                        {
                            _status.Text      = msg;
                            _status.ForeColor = isError
                                ? Color.FromArgb(203, 40, 40)
                                : Color.FromArgb(130, 142, 158);
                        });
                    }
                    catch { }
                }
            }

            private static void ClearDrag()
            {
                _dragPiece    = null;
                _dragSource   = null;
                _dragRotated  = false;
                _rKeyWasDown  = false;
                _rightWasDown = false;
            }
        }
    }
}
