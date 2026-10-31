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
        // card — picture, its sizes beside it — is visible without scrolling.
        private const int DiagramW = 200;
        private const int DiagramH = 400;
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
            ClientSize    = new Size(1120, 640);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            root.Controls.Add(BuildHeader(plan), 0, 0);
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

            var title = new Label
            {
                Text      = "⛁  Sheet cutting plan",
                Font      = FTitle,
                ForeColor = CText,
                AutoSize  = true,
                Location  = new Point(14, 10)
            };

            string summary = plan.Sheets.Count == 0
                ? "Nothing to cut — add the sizes on the bill first."
                : $"{plan.Sheets.Count} sheet{(plan.Sheets.Count == 1 ? "" : "s")} of {sheetSize}   ·   " +
                  $"{plan.PieceCount} pieces   ·   {plan.OverallUsedPercent:0.#}% of the plywood used" +
                  "   ·   dashed = unused";

            var line = new Label
            {
                Text        = summary,
                Font        = FLine,
                ForeColor   = plan.Sheets.Count == 0 ? CSub : CGreen,
                AutoSize    = true,
                MaximumSize = new Size(700, 0),
                Location    = new Point(16, 42)
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
                    Location     = new Point(16, 64)
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
            int cardH = DiagramH + 52;

            var card = new Panel { Size = new Size(cardW, cardH), Margin = new Padding(10), BackColor = CCard };
            card.Paint += (_, e) => PaintBorder(e.Graphics, card);

            // "Sheet 3" on the left, its usage pinned to the right edge of the card. The label
            // spans the card rather than half of it, otherwise the trailing "% used" is clipped.
            var heading = new Label
            {
                Text      = $"Sheet {sheet.Number}",
                Font      = FSheet,
                ForeColor = CText,
                Size      = new Size(96, 18),
                Location  = new Point(12, 8)
            };

            var used = new Label
            {
                Text      = $"{sheet.Placements.Count} pieces · {sheet.UsedPercent:0.#}% used",
                Font      = FMeta,
                ForeColor = sheet.UsedPercent >= 80 ? CGreen : CSub,
                TextAlign = ContentAlignment.TopRight,
                Size      = new Size(cardW - 24, 18),
                Location  = new Point(12, 11)
            };

            var diagram = new SheetDiagram(sheet)
            {
                Location = new Point(12, 32),
                Size     = new Size(DiagramW, DiagramH)
            };

            var legend = new Label
            {
                Text          = LegendFor(sheet),
                Font          = FLegend,
                ForeColor     = CText,
                AutoSize      = false,
                Size          = new Size(LegendW, DiagramH),
                Location      = new Point(12 + DiagramW + 16, 34)
            };

            card.Controls.Add(heading);
            card.Controls.Add(used);
            card.Controls.Add(diagram);
            card.Controls.Add(legend);
            return card;
        }

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
