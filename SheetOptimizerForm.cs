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

        // A sheet is 48 × 96 inches, so the picture keeps that 1 : 2 shape. Sized so a whole
        // card — picture, its sizes beside it — is visible without scrolling, and big enough
        // that the numbers written on the pieces are readable rather than a smear.
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

            string sheetSize = $"{_fmt.FormatCompactFromEighths(plan.SheetHeightEighths)} × " +
                               $"{_fmt.FormatCompactFromEighths(plan.SheetWidthEighths)} in " +
                               $"(8 ft × 4 ft)";

            string titleText = "⛁  Sheet cutting plan";

            string summary = plan.Sheets.Count == 0
                ? "Nothing to cut — add the sizes on the bill first."
                : $"{plan.Sheets.Count} sheet{(plan.Sheets.Count == 1 ? "" : "s")} of {sheetSize}   ·   " +
                  $"{plan.PieceCount} pieces   ·   {plan.OverallUsedPercent:0.#}% of the plywood used" +
                  "   ·   dashed = unused";

            // When the bill named its sheets, say how the plan is split between them.
            var bySource = plan.Sheets
                               .GroupBy(s => s.Source)
                               .Where(g => g.Key.Length > 0)
                               .ToList();
            if (bySource.Count > 1)
                summary += "\n" + string.Join("   ·   ", bySource.Select(g =>
                    $"{g.Key}: {g.Count()} sheet{(g.Count() == 1 ? "" : "s")} (no. {g.Min(s => s.Number)}–{g.Max(s => s.Number)})"));

            // The card is as tall as the text it has to hold: the title line, the summary —
            // two lines once the plan is split across materials — and the oversized-piece note.
            int titleH       = Math.Max(30, Measure(titleText, FTitle).Height + 6);
            int lineH        = Math.Max(22, Measure("0", FLine).Height + 6);
            int metaH        = Math.Max(20, Measure("0", FMeta).Height + 6);
            int summaryLines = bySource.Count > 1 ? 2 : 1;

            int titleY  = 10;
            int lineY   = titleY + titleH + 4;
            int warnY   = lineY + summaryLines * lineH + 2;
            card.Height = warnY + (plan.Unplaced.Count > 0 ? metaH + 2 : 0) + 12;

            var title = new Label
            {
                Text      = titleText,
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = true,
                Location  = new Point(14, titleY)
            };

            var line = new Label
            {
                Text        = summary,
                Font        = FLine,
                ForeColor   = plan.Sheets.Count == 0 ? CSub : CGreen,
                AutoSize    = true,
                MaximumSize = new Size(700, 0),
                Location    = new Point(16, lineY)
            };

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
            card.Controls.Add(line);
            card.Controls.Add(savePdf);
            card.Controls.Add(back);

            if (plan.Unplaced.Count > 0)
            {
                var warn = new Label
                {
                    Text         = $"⚠  {plan.Unplaced.Count} piece(s) are bigger than a sheet and cannot be cut: " +
                                   string.Join(", ", plan.Unplaced.Select(u =>
                                       $"{_fmt.FormatCompactFromEighths(u.HeightEighths)} × " +
                                       $"{_fmt.FormatCompactFromEighths(u.WidthEighths)} (row {u.SerialNumber})")),
                    Font         = FMeta,
                    ForeColor    = CRed,
                    AutoSize     = true,
                    MaximumSize  = new Size(820, 0),
                    Location     = new Point(16, warnY)
                };
                card.Controls.Add(warn);
            }

            return card;
        }

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
            int cardW = DiagramW + LegendW + 40;

            string headingText = $"Sheet {sheet.Number}" +
                                 (string.IsNullOrWhiteSpace(sheet.Source) ? "" : $"  ·  {sheet.Source}");
            string usedText    = $"{sheet.Placements.Count} pieces · {sheet.UsedPercent:0.#}% used";

            // Both halves of the heading are measured. A fixed 18 px row shaves the bottom off
            // "Sheet 1", and a label allowed only half the card hides the material code the
            // plan was grouped by — which is the one thing the shop needs to see here.
            int headingH = Math.Max(24, Measure(headingText, FSheet).Height + 6);
            int usedW    = Math.Min(cardW - 60, Measure(usedText, FMeta).Width + 8);
            int cardH    = DiagramH + headingH + 20;

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
                Size     = new Size(DiagramW, DiagramH)
            };

            var legend = new Label
            {
                Text          = LegendFor(sheet),
                Font          = FLegend,
                ForeColor     = CText,
                AutoSize      = false,
                Size          = new Size(LegendW, DiagramH),
                Location      = new Point(12 + DiagramW + 16, diagramTop + 2)
            };

            card.Controls.Add(heading);
            card.Controls.Add(used);
            card.Controls.Add(diagram);
            card.Controls.Add(legend);
            return card;
        }

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
                                 TypedHeight = p.Rotated ? p.WidthEighths : p.HeightEighths,
                                 TypedWidth  = p.Rotated ? p.HeightEighths : p.WidthEighths
                             })
                             .OrderBy(g => g.Key.SerialNumber)
                             .Select(g =>
                             {
                                 string size = $"{_fmt.FormatCompactFromEighths(g.Key.TypedHeight)} × " +
                                               $"{_fmt.FormatCompactFromEighths(g.Key.TypedWidth)}";
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
